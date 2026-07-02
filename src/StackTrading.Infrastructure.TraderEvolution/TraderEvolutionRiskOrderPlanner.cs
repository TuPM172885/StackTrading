using StackTrading.Contracts;

namespace StackTrading.Infrastructure.TraderEvolution;

public static class TraderEvolutionRiskOrderPlanner
{
    public static IReadOnlyList<OrderRequest> PlanFlattenOrders(
        string accountId,
        RiskActionRequest request,
        IReadOnlyCollection<Position> positions)
    {
        return positions
            .Where(position => position.Side != PositionSide.Flat && position.Quantity > 0)
            .OrderBy(position => position.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(position => CreateRiskOrder(accountId, request, position, position.Quantity, "flatten"))
            .ToList();
    }

    public static IReadOnlyList<OrderRequest> PlanTrimOrders(
        string accountId,
        RiskActionRequest request,
        IReadOnlyCollection<Position> positions)
    {
        if (request.TargetLimit is null)
        {
            throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, "TrimToCompliance requires TargetLimit.");
        }

        if (request.TargetLimit < 0)
        {
            throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, "TrimToCompliance TargetLimit must be greater than or equal to zero.");
        }

        var openPositions = positions
            .Where(position => position.Side != PositionSide.Flat && position.Quantity > 0)
            .OrderByDescending(position => position.Quantity)
            .ThenBy(position => position.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var currentExposure = openPositions.Sum(position => position.Quantity);
        var excessExposure = currentExposure - request.TargetLimit.Value;
        if (excessExposure <= 0)
        {
            return [];
        }

        var orders = new List<OrderRequest>();
        foreach (var position in openPositions)
        {
            if (excessExposure <= 0)
            {
                break;
            }

            var reduceQuantity = Math.Min(position.Quantity, excessExposure);
            orders.Add(CreateRiskOrder(accountId, request, position, reduceQuantity, "trim"));
            excessExposure -= reduceQuantity;
        }

        return orders;
    }

    private static OrderRequest CreateRiskOrder(
        string accountId,
        RiskActionRequest request,
        Position position,
        decimal quantity,
        string riskAction)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["riskAction"] = riskAction,
            ["riskReason"] = request.Reason,
            ["requestedBy"] = request.RequestedBy
        };

        if (request.TargetLimit is not null)
        {
            metadata["targetLimit"] = request.TargetLimit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (request.Metadata is not null)
        {
            foreach (var item in request.Metadata)
            {
                metadata[item.Key] = item.Value;
            }
        }

        return new OrderRequest(
            request.CorrelationId,
            accountId,
            request.Environment,
            position.Symbol,
            position.Side == PositionSide.Long ? OrderSide.Sell : OrderSide.Buy,
            OrderType.Market,
            quantity,
            LimitPrice: null,
            StopPrice: null,
            TimeInForce.Day,
            metadata);
    }
}
