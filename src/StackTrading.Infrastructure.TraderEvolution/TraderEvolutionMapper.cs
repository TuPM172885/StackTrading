using System.Globalization;
using System.Text.Json;
using StackTrading.Contracts;

namespace StackTrading.Infrastructure.TraderEvolution;

public static class TraderEvolutionMapper
{
    public static BrokerAccount ToDomain(TraderEvolutionAccountDto dto, TradingEnv fallbackEnvironment) =>
        new(
            Require(dto.AccountId, nameof(dto.AccountId)),
            Require(dto.ExternalUserId, nameof(dto.ExternalUserId)),
            ParseEnum(dto.Environment, fallbackEnvironment),
            ParseEnum(dto.Status, AccountStatus.Active),
            Require(dto.BaseCurrency, nameof(dto.BaseCurrency)),
            dto.Balance,
            dto.Equity,
            dto.CreatedAt == default ? DateTimeOffset.UtcNow : dto.CreatedAt,
            dto.Metadata);

    public static OrderResult ToDomain(TraderEvolutionOrderDto dto, TradingEnv fallbackEnvironment) =>
        new(
            Require(dto.OrderId, nameof(dto.OrderId)),
            Require(dto.AccountId, nameof(dto.AccountId)),
            ParseEnum(dto.Environment, fallbackEnvironment),
            ParseOrderStatus(dto.Status),
            Require(dto.Symbol, nameof(dto.Symbol)),
            ParseEnum(dto.Side, OrderSide.Buy),
            dto.Quantity,
            dto.FilledQuantity,
            dto.AverageFillPrice,
            dto.BrokerMessage,
            dto.OccurredAt == default ? DateTimeOffset.UtcNow : dto.OccurredAt);

    public static Position ToDomain(TraderEvolutionPositionDto dto) =>
        new(
            Require(dto.AccountId, nameof(dto.AccountId)),
            Require(dto.Symbol, nameof(dto.Symbol)),
            ParsePositionSide(dto.Side, dto.Quantity),
            Math.Abs(dto.Quantity),
            dto.AveragePrice,
            dto.UnrealizedPnl,
            dto.EstimatedSwap,
            dto.UpdatedAt == default ? DateTimeOffset.UtcNow : dto.UpdatedAt);

    public static AccountState ToDomain(TraderEvolutionAccountStateDto dto, TradingEnv fallbackEnvironment) =>
        new(
            Require(dto.AccountId, nameof(dto.AccountId)),
            ParseEnum(dto.Environment, fallbackEnvironment),
            ParseEnum(dto.Status, AccountStatus.Active),
            dto.Balance,
            dto.Equity,
            dto.MarginUsed,
            dto.MarginAvailable,
            dto.DailyLoss,
            dto.TrailingDrawdown,
            dto.UpdatedAt == default ? DateTimeOffset.UtcNow : dto.UpdatedAt);

    public static BrokerEvent ToDomain(TraderEvolutionBrokerEventDto dto, TradingEnv fallbackEnvironment) =>
        new(
            ParseBrokerEventType(dto.EventType),
            Require(dto.AccountId, nameof(dto.AccountId)),
            ParseEnum(dto.Environment, fallbackEnvironment),
            Require(dto.CorrelationId, nameof(dto.CorrelationId)),
            Require(dto.IdempotencyKey, nameof(dto.IdempotencyKey)),
            dto.OccurredAt == default ? DateTimeOffset.UtcNow : dto.OccurredAt,
            dto.Payload.ValueKind == JsonValueKind.Undefined ? JsonSerializer.SerializeToElement(new { }) : dto.Payload);

    private static OrderStatus ParseOrderStatus(JsonElement value)
    {
        var token = ReadToken(value);
        return token switch
        {
            "new" or "open" or "working" or "accepted" => OrderStatus.Accepted,
            "partial" or "partiallyfilled" or "partially_filled" => OrderStatus.PartiallyFilled,
            "filled" or "done" => OrderStatus.Filled,
            "cancelled" or "canceled" => OrderStatus.Cancelled,
            "rejected" or "failed" => OrderStatus.Rejected,
            "pending" => OrderStatus.Pending,
            _ => ParseEnum(value, OrderStatus.Pending)
        };
    }

    private static PositionSide ParsePositionSide(JsonElement value, decimal quantity)
    {
        var token = ReadToken(value);
        return token switch
        {
            "buy" or "long" => PositionSide.Long,
            "sell" or "short" => PositionSide.Short,
            "flat" => PositionSide.Flat,
            _ when quantity > 0 => PositionSide.Long,
            _ when quantity < 0 => PositionSide.Short,
            _ => PositionSide.Flat
        };
    }

    private static BrokerEventType ParseBrokerEventType(JsonElement value)
    {
        var token = ReadToken(value);
        return token switch
        {
            "orderaccepted" or "order_accepted" or "order.accepted" => BrokerEventType.OrderAccepted,
            "orderfilled" or "order_filled" or "order.filled" or "execution" => BrokerEventType.OrderFilled,
            "ordercancelled" or "ordercanceled" or "order_cancelled" or "order.canceled" => BrokerEventType.OrderCancelled,
            "orderrejected" or "order_rejected" or "order.rejected" => BrokerEventType.OrderRejected,
            "positionupdated" or "position_updated" or "position.updated" => BrokerEventType.PositionUpdated,
            "accountstatechanged" or "account_state_changed" or "account.changed" => BrokerEventType.AccountStateChanged,
            "executionreport" or "execution_report" => BrokerEventType.ExecutionReport,
            "marginbreach" or "margin_breach" => BrokerEventType.MarginBreach,
            "drawdownbreach" or "drawdown_breach" => BrokerEventType.DrawdownBreach,
            "liquidationexecuted" or "liquidation_executed" => BrokerEventType.LiquidationExecuted,
            _ => ParseEnum(value, BrokerEventType.ExecutionReport)
        };
    }

    private static TEnum ParseEnum<TEnum>(JsonElement value, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return Enum.IsDefined(typeof(TEnum), number) ? (TEnum)Enum.ToObject(typeof(TEnum), number) : fallback;
        }

        var token = ReadToken(value);
        return Enum.TryParse<TEnum>(token, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static string ReadToken(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return Normalize(value.GetString());
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.GetRawText();
        }

        return string.Empty;
    }

    private static string Normalize(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToLower(CultureInfo.InvariantCulture);

    private static string Require(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, $"TraderEvolution response is missing required field '{fieldName}'.");
        }

        return value;
    }
}
