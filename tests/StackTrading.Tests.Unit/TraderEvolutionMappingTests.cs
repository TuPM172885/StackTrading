using System.Net;
using System.Text.Json;
using FluentAssertions;
using StackTrading.Contracts;
using StackTrading.Infrastructure.TraderEvolution;

namespace StackTrading.Tests.Unit;

public sealed class TraderEvolutionMappingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ToDomain_ShouldNormalizeOrderStatusAliases()
    {
        var dto = JsonSerializer.Deserialize<TraderEvolutionOrderDto>(
            """
            {
              "orderId": "ORD-1",
              "accountId": "ACC-1",
              "environment": "paper",
              "status": "working",
              "symbol": "EURUSD",
              "side": "buy",
              "quantity": 2,
              "filledQuantity": 0,
              "occurredAt": "2026-07-02T00:00:00Z"
            }
            """,
            JsonOptions);

        var result = TraderEvolutionMapper.ToDomain(dto!, TradingEnv.Paper);

        result.Environment.Should().Be(TradingEnv.Paper);
        result.Status.Should().Be(OrderStatus.Accepted);
        result.Side.Should().Be(OrderSide.Buy);
    }

    [Fact]
    public void ToDomain_ShouldDerivePositionSideFromNegativeQuantity_WhenSideMissing()
    {
        var dto = JsonSerializer.Deserialize<TraderEvolutionPositionDto>(
            """
            {
              "accountId": "ACC-1",
              "symbol": "NQ",
              "quantity": -3,
              "averagePrice": 20000,
              "unrealizedPnl": 42,
              "estimatedSwap": 0,
              "updatedAt": "2026-07-02T00:00:00Z"
            }
            """,
            JsonOptions);

        var result = TraderEvolutionMapper.ToDomain(dto!);

        result.Side.Should().Be(PositionSide.Short);
        result.Quantity.Should().Be(3m);
    }

    [Fact]
    public void ToDomain_ShouldNormalizeAccountState()
    {
        var dto = JsonSerializer.Deserialize<TraderEvolutionAccountStateDto>(
            """
            {
              "accountId": "ACC-1",
              "environment": "paper",
              "status": "active",
              "balance": 100000,
              "equity": 100250,
              "marginUsed": 2500,
              "marginAvailable": 97750,
              "dailyLoss": 0,
              "trailingDrawdown": 1000,
              "updatedAt": "2026-07-02T00:00:00Z"
            }
            """,
            JsonOptions);

        var result = TraderEvolutionMapper.ToDomain(dto!, TradingEnv.Paper);

        result.Status.Should().Be(AccountStatus.Active);
        result.MarginAvailable.Should().Be(97750m);
        result.TrailingDrawdown.Should().Be(1000m);
    }

    [Fact]
    public void ToDomain_ShouldNormalizeBrokerEventAliases()
    {
        var dto = JsonSerializer.Deserialize<TraderEvolutionBrokerEventDto>(
            """
            {
              "eventType": "order.accepted",
              "accountId": "ACC-1",
              "environment": "paper",
              "correlationId": "corr-1",
              "idempotencyKey": "evt-1",
              "occurredAt": "2026-07-02T00:00:00Z",
              "payload": { "orderId": "ORD-1" }
            }
            """,
            JsonOptions);

        var result = TraderEvolutionMapper.ToDomain(dto!, TradingEnv.Paper);

        result.EventType.Should().Be(BrokerEventType.OrderAccepted);
        result.Payload.GetProperty("orderId").GetString().Should().Be("ORD-1");
    }

    [Fact]
    public void ToException_ShouldMapBrokerErrorBodyToDomainCode()
    {
        var exception = TraderEvolutionBrokerErrorMapper.ToException(
            HttpStatusCode.BadRequest,
            "Bad Request",
            """{ "code": "VALIDATION_FAILED", "message": "quantity must be positive" }""");

        exception.Code.Should().Be(BrokerErrorCode.ValidationFailed);
        exception.Message.Should().Contain("quantity must be positive");
    }
}
