using System.Text.Json;
using FluentAssertions;
using StackTrading.Contracts;
using StackTrading.Infrastructure.TraderEvolution;

namespace StackTrading.Tests.Unit;

public sealed class TraderEvolutionStreamMappingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // --- MapOrderStreamEvents via ToDomainOrderRow ---

    [Theory]
    [InlineData("filled", BrokerEventType.OrderFilled)]
    [InlineData("done", BrokerEventType.OrderFilled)]
    [InlineData("partial", BrokerEventType.OrderFilled)]
    [InlineData("cancelled", BrokerEventType.OrderCancelled)]
    [InlineData("canceled", BrokerEventType.OrderCancelled)]
    [InlineData("rejected", BrokerEventType.OrderRejected)]
    [InlineData("failed", BrokerEventType.OrderRejected)]
    [InlineData("working", BrokerEventType.OrderAccepted)]
    [InlineData("accepted", BrokerEventType.OrderAccepted)]
    [InlineData("pending", BrokerEventType.OrderAccepted)]
    public void ToDomainOrderRow_ShouldMapStatusToCorrectEventType(string rawStatus, BrokerEventType expectedEventType)
    {
        var row = JsonSerializer.Deserialize<JsonElement>(
            $$"""{"orderId":"ORD-1","accountId":"ACC-1","status":"{{rawStatus}}","symbol":"EURUSD","side":"buy","quantity":1}""",
            JsonOptions);

        var order = TraderEvolutionMapper.ToDomainOrderRow("ACC-1", TradingEnv.Paper, row);

        var eventType = order.Status switch
        {
            OrderStatus.Filled or OrderStatus.PartiallyFilled => BrokerEventType.OrderFilled,
            OrderStatus.Cancelled => BrokerEventType.OrderCancelled,
            OrderStatus.Rejected => BrokerEventType.OrderRejected,
            _ => BrokerEventType.OrderAccepted
        };

        eventType.Should().Be(expectedEventType);
    }

    [Fact]
    public void ToDomainOrderRow_ShouldMapObjectRowFromStream()
    {
        var row = JsonSerializer.Deserialize<JsonElement>(
            """{"orderId":"ORD-99","accountId":"ACC-1","status":"filled","symbol":"NQ","side":"sell","quantity":3,"filledQuantity":3,"averageFillPrice":19000}""",
            JsonOptions);

        var order = TraderEvolutionMapper.ToDomainOrderRow("ACC-1", TradingEnv.Paper, row);

        order.OrderId.Should().Be("ORD-99");
        order.Symbol.Should().Be("NQ");
        order.Side.Should().Be(OrderSide.Sell);
        order.Status.Should().Be(OrderStatus.Filled);
        order.Quantity.Should().Be(3m);
        order.AverageFillPrice.Should().Be(19000m);
    }

    // --- ParseBrokerEventType via ToDomain(TraderEvolutionBrokerEventDto) ---

    [Theory]
    [InlineData("order.accepted", BrokerEventType.OrderAccepted)]
    [InlineData("order_filled", BrokerEventType.OrderFilled)]
    [InlineData("execution", BrokerEventType.OrderFilled)]
    [InlineData("order.canceled", BrokerEventType.OrderCancelled)]
    [InlineData("order_rejected", BrokerEventType.OrderRejected)]
    [InlineData("position.updated", BrokerEventType.PositionUpdated)]
    [InlineData("account.changed", BrokerEventType.AccountStateChanged)]
    [InlineData("execution_report", BrokerEventType.ExecutionReport)]
    [InlineData("margin_breach", BrokerEventType.MarginBreach)]
    [InlineData("drawdown_breach", BrokerEventType.DrawdownBreach)]
    [InlineData("liquidation_executed", BrokerEventType.LiquidationExecuted)]
    public void ToDomain_BrokerEventDto_ShouldMapEventTypeAliases(string rawEventType, BrokerEventType expected)
    {
        var dto = JsonSerializer.Deserialize<TraderEvolutionBrokerEventDto>(
            $$"""
            {
              "eventType": "{{rawEventType}}",
              "accountId": "ACC-1",
              "correlationId": "corr-1",
              "idempotencyKey": "key-1",
              "occurredAt": "2026-07-07T00:00:00Z",
              "payload": {}
            }
            """,
            JsonOptions);

        var result = TraderEvolutionMapper.ToDomain(dto!, TradingEnv.Paper);

        result.EventType.Should().Be(expected);
    }

    // --- Position row from stream ---

    [Fact]
    public void ToDomainPositionRow_ShouldMapObjectFromStream()
    {
        var row = JsonSerializer.Deserialize<JsonElement>(
            """{"symbol":"EURUSD","side":"buy","quantity":2,"averagePrice":1.1,"unrealizedPnl":50}""",
            JsonOptions);

        var position = TraderEvolutionMapper.ToDomainPositionRow("ACC-1", row);

        position.AccountId.Should().Be("ACC-1");
        position.Symbol.Should().Be("EURUSD");
        position.Side.Should().Be(PositionSide.Long);
        position.Quantity.Should().Be(2m);
        position.AveragePrice.Should().Be(1.1m);
        position.UnrealizedPnl.Should().Be(50m);
    }

    [Fact]
    public void ToDomainPositionRow_ShouldDeriveSideFromQuantitySign_WhenSideFieldAbsent()
    {
        var row = JsonSerializer.Deserialize<JsonElement>(
            """{"symbol":"NQ","quantity":-5,"averagePrice":20000,"unrealizedPnl":-100}""",
            JsonOptions);

        var position = TraderEvolutionMapper.ToDomainPositionRow("ACC-1", row);

        position.Side.Should().Be(PositionSide.Short);
        position.Quantity.Should().Be(5m);
    }

    // --- Account state array ---

    [Fact]
    public void ToDomainAccountState_ShouldFallBackToBalancePlusOpenPnl_WhenProjectedBalanceIsZero()
    {
        // index 0 = balance, index 1 = projectedBalance (0 = absent), index 23 = openNetPnl
        var values = new decimal[26];
        values[0] = 100_000m;
        values[1] = 0m;
        values[23] = 500m;

        var json = "[" + string.Join(",", values) + "]";
        var state = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

        var result = TraderEvolutionMapper.ToDomainAccountState("ACC-1", TradingEnv.Paper, state);

        result.Equity.Should().Be(100_500m);
    }
}
