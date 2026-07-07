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
    public void ToDomain_ShouldMapAccountSettingsFromClientApi()
    {
        var dto = JsonSerializer.Deserialize<TraderEvolutionAccountSettingsDto>(
            """
            {
              "id": "3243753",
              "name": "test",
              "type": "demo",
              "currency": "USD",
              "status": "ACTIVE",
              "tradingRules": { "supportBrackets": true },
              "riskRules": { "maxTrailingDrawdown": 800 },
              "marginRules": { "stopOutLevel": 100 }
            }
            """,
            JsonOptions);

        var request = new ProvisionRequest("corr-1", "user-1", "challenge-1", "USD", 100000m);

        var result = TraderEvolutionMapper.ToDomain(dto!, request, TradingEnv.Paper);

        result.AccountId.Should().Be("3243753");
        result.ExternalUserId.Should().Be("user-1");
        result.Status.Should().Be(AccountStatus.Active);
        result.Metadata.Should().ContainKey("riskRules");
    }

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
    public void ToDomainOrderRow_ShouldMapArrayUsingDefaultOrdersConfig()
    {
        var row = JsonSerializer.Deserialize<JsonElement>(
            """
            [9001, 1001, 2, "Buy", "Market", "working", 1, 1.2345, null, null, "Day", null, 1783324800000, 1783324800000, true, null, null, null, null, null, null, "EURUSD", "forex", "FX"]
            """,
            JsonOptions);

        var result = TraderEvolutionMapper.ToDomainOrderRow("ACC-1", TradingEnv.Paper, row);

        result.OrderId.Should().Be("9001");
        result.AccountId.Should().Be("ACC-1");
        result.Symbol.Should().Be("EURUSD");
        result.Side.Should().Be(OrderSide.Buy);
        result.Status.Should().Be(OrderStatus.Accepted);
        result.Quantity.Should().Be(2m);
        result.FilledQuantity.Should().Be(1m);
        result.AverageFillPrice.Should().Be(1.2345m);
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
    public void ToDomainPositionRow_ShouldMapArrayUsingDefaultPositionsConfig()
    {
        var row = JsonSerializer.Deserialize<JsonElement>(
            """
            [101, 555, "Sell", 3, 20000, null, null, 1783324800000, 42, null, "NQ", "futures", "CME", null, null, 0, 1.25]
            """,
            JsonOptions);

        var result = TraderEvolutionMapper.ToDomainPositionRow("ACC-1", row);

        result.AccountId.Should().Be("ACC-1");
        result.Symbol.Should().Be("NQ");
        result.Side.Should().Be(PositionSide.Short);
        result.Quantity.Should().Be(3m);
        result.AveragePrice.Should().Be(20000m);
        result.UnrealizedPnl.Should().Be(42m);
        result.EstimatedSwap.Should().Be(1.25m);
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

    [Fact]
    public void ToDomainAccountState_ShouldMapArrayUsingDefaultAccountDetailsConfig()
    {
        var state = JsonSerializer.Deserialize<JsonElement>(
            """
            [100000, 100250, 97750, 0, 100000, 0, 97750, 0, 0, 2500, 2400, 0, 0, 0, 100, 0, 97500, 0, -50, 1, 10, 2, 250, 200, 1, 1]
            """,
            JsonOptions);

        var result = TraderEvolutionMapper.ToDomainAccountState("ACC-1", TradingEnv.Paper, state);

        result.AccountId.Should().Be("ACC-1");
        result.Balance.Should().Be(100000m);
        result.Equity.Should().Be(100250m);
        result.MarginUsed.Should().Be(2500m);
        result.MarginAvailable.Should().Be(97750m);
    }


    [Fact]
    public void ToException_ShouldReadTraderEvolutionEnvelopeErrmsg()
    {
        var exception = TraderEvolutionBrokerErrorMapper.ToException(
            HttpStatusCode.Unauthorized,
            "Unauthorized",
            """{ "s": "error", "errmsg": "Invalid token" }""");

        exception.Code.Should().Be(BrokerErrorCode.AuthenticationFailed);
        exception.Message.Should().Contain("Invalid token");
    }
}
