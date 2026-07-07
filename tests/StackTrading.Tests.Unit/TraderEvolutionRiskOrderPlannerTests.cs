using FluentAssertions;
using StackTrading.Contracts;
using StackTrading.Infrastructure.TraderEvolution;

namespace StackTrading.Tests.Unit;

public sealed class TraderEvolutionRiskOrderPlannerTests
{
    [Fact]
    public void PlanFlattenOrders_ShouldCreateOppositeMarketOrdersForOpenPositions()
    {
        var request = CreateRiskRequest(targetLimit: null);
        var positions = new[]
        {
            CreatePosition("ACC-1", "NQ", PositionSide.Long, 2m),
            CreatePosition("ACC-1", "ES", PositionSide.Short, 3m),
            CreatePosition("ACC-1", "EURUSD", PositionSide.Flat, 0m)
        };

        var orders = TraderEvolutionRiskOrderPlanner.PlanFlattenOrders("ACC-1", request, positions);

        orders.Should().HaveCount(2);
        orders.Should().Contain(order => order.Symbol == "NQ" && order.Side == OrderSide.Sell && order.Quantity == 2m);
        orders.Should().Contain(order => order.Symbol == "ES" && order.Side == OrderSide.Buy && order.Quantity == 3m);
        orders.Should().OnlyContain(order => order.Type == OrderType.Market && order.TimeInForce == TimeInForce.Day);
        orders.Should().OnlyContain(order => order.Metadata!["riskAction"] == "flatten");
        orders.Should().OnlyContain(order => order.Metadata!["riskReason"] == "compliance breach");
        orders.Should().OnlyContain(order => order.Metadata!["requestedBy"] == "risk-engine");
    }

    [Fact]
    public void PlanTrimOrders_ShouldReduceLargestPositionsUntilTargetExposure()
    {
        var request = CreateRiskRequest(targetLimit: 5m);
        var positions = new[]
        {
            CreatePosition("ACC-1", "NQ", PositionSide.Long, 6m),
            CreatePosition("ACC-1", "ES", PositionSide.Short, 3m),
            CreatePosition("ACC-1", "YM", PositionSide.Long, 1m)
        };

        var orders = TraderEvolutionRiskOrderPlanner.PlanTrimOrders("ACC-1", request, positions);

        orders.Should().HaveCount(1);
        orders[0].Symbol.Should().Be("NQ");
        orders[0].Side.Should().Be(OrderSide.Sell);
        orders[0].Quantity.Should().Be(5m);
        orders[0].Metadata!["riskAction"].Should().Be("trim");
        orders[0].Metadata!["targetLimit"].Should().Be("5");
    }

    [Fact]
    public void PlanTrimOrders_ShouldCreateMultipleOrders_WhenLargestPositionIsNotEnough()
    {
        var request = CreateRiskRequest(targetLimit: 1m);
        var positions = new[]
        {
            CreatePosition("ACC-1", "NQ", PositionSide.Long, 3m),
            CreatePosition("ACC-1", "ES", PositionSide.Short, 2m)
        };

        var orders = TraderEvolutionRiskOrderPlanner.PlanTrimOrders("ACC-1", request, positions);

        orders.Should().HaveCount(2);
        orders[0].Should().Match<OrderRequest>(order => order.Symbol == "NQ" && order.Quantity == 3m && order.Side == OrderSide.Sell);
        orders[1].Should().Match<OrderRequest>(order => order.Symbol == "ES" && order.Quantity == 1m && order.Side == OrderSide.Buy);
    }

    [Fact]
    public void PlanTrimOrders_ShouldRejectMissingTargetLimit()
    {
        var request = CreateRiskRequest(targetLimit: null);
        var positions = new[] { CreatePosition("ACC-1", "NQ", PositionSide.Long, 1m) };

        var act = () => TraderEvolutionRiskOrderPlanner.PlanTrimOrders("ACC-1", request, positions);

        act.Should().Throw<BrokerAdapterException>()
            .Which.Code.Should().Be(BrokerErrorCode.ValidationFailed);
    }

    [Fact]
    public void PlanTrimOrders_ShouldReturnEmpty_WhenAlreadyCompliant()
    {
        var request = CreateRiskRequest(targetLimit: 10m);
        var positions = new[]
        {
            CreatePosition("ACC-1", "NQ", PositionSide.Long, 3m),
            CreatePosition("ACC-1", "ES", PositionSide.Short, 2m)
        };

        var orders = TraderEvolutionRiskOrderPlanner.PlanTrimOrders("ACC-1", request, positions);

        orders.Should().BeEmpty();
    }

    [Fact]
    public void PlanFlattenOrders_ShouldReturnEmpty_WhenNoOpenPositions()
    {
        var request = CreateRiskRequest(targetLimit: null);
        var positions = new[]
        {
            CreatePosition("ACC-1", "NQ", PositionSide.Flat, 0m),
            CreatePosition("ACC-1", "ES", PositionSide.Flat, 0m)
        };

        var orders = TraderEvolutionRiskOrderPlanner.PlanFlattenOrders("ACC-1", request, positions);

        orders.Should().BeEmpty();
    }

    [Fact]
    public void PlanFlattenOrders_ShouldIncludeAuditMetadata()
    {
        var request = CreateRiskRequest(targetLimit: null);
        var positions = new[] { CreatePosition("ACC-1", "NQ", PositionSide.Long, 1m) };

        var orders = TraderEvolutionRiskOrderPlanner.PlanFlattenOrders("ACC-1", request, positions);

        orders.Should().ContainSingle();
        orders[0].Metadata!["correlationId"].Should().Be("corr-risk-1");
        orders[0].Metadata!["riskEnv"].Should().Be("Paper");
    }

    private static RiskActionRequest CreateRiskRequest(decimal? targetLimit) =>
        new(
            "corr-risk-1",
            TradingEnv.Paper,
            "compliance breach",
            "risk-engine",
            targetLimit,
            new Dictionary<string, string> { ["ticket"] = "RISK-1" });

    private static Position CreatePosition(string accountId, string symbol, PositionSide side, decimal quantity) =>
        new(accountId, symbol, side, quantity, 100m, 0m, 0m, DateTimeOffset.UtcNow);
}
