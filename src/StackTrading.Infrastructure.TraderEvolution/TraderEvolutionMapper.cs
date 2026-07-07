using System.Globalization;
using System.Text.Json;
using StackTrading.Contracts;

namespace StackTrading.Infrastructure.TraderEvolution;

public static class TraderEvolutionMapper
{
    public static BrokerAccount ToDomain(TraderEvolutionAccountSettingsDto dto, ProvisionRequest request, TradingEnv env)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["accountName"] = dto.Name,
            ["accountType"] = dto.Type
        };

        AddJsonMetadata(metadata, "tradingRules", dto.TradingRules);
        AddJsonMetadata(metadata, "riskRules", dto.RiskRules);
        AddJsonMetadata(metadata, "marginRules", dto.MarginRules);

        if (request.Metadata is not null)
        {
            foreach (var item in request.Metadata)
            {
                metadata[item.Key] = item.Value;
            }
        }

        return new BrokerAccount(
            Require(dto.Id, nameof(dto.Id)),
            request.ExternalUserId,
            env,
            ParseAccountStatus(dto.Status),
            string.IsNullOrWhiteSpace(dto.Currency) ? request.BaseCurrency : dto.Currency,
            request.StartingBalance,
            request.StartingBalance,
            DateTimeOffset.UtcNow,
            metadata);
    }

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

    public static OrderResult ToDomainOrderRow(string accountId, TradingEnv env, JsonElement row)
    {
        if (row.ValueKind == JsonValueKind.Object)
        {
            var dto = row.Deserialize<TraderEvolutionOrderDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, "TraderEvolution order payload is empty.");

            return ToDomain(dto, env);
        }

        if (row.ValueKind != JsonValueKind.Array)
        {
            throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, "TraderEvolution order row must be an array or object.");
        }

        var values = row.EnumerateArray().ToArray();
        var orderId = ReadString(values.ElementAtOrDefault(0));
        var quantity = ReadDecimal(values.ElementAtOrDefault(2));
        var side = ParseEnum(values.ElementAtOrDefault(3), OrderSide.Buy);
        var status = ParseOrderStatus(values.ElementAtOrDefault(5));
        var symbol = ReadString(values.ElementAtOrDefault(21));
        var occurredAt = ReadUnixMilliseconds(values.ElementAtOrDefault(12)) ?? DateTimeOffset.UtcNow;

        return new OrderResult(
            Require(orderId, "orderId"),
            accountId,
            env,
            status,
            string.IsNullOrWhiteSpace(symbol) ? ReadString(values.ElementAtOrDefault(1)) : symbol,
            side,
            quantity,
            ReadDecimal(values.ElementAtOrDefault(6)),
            ReadNullableDecimal(values.ElementAtOrDefault(7)),
            BrokerMessage: null,
            occurredAt);
    }

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

    public static Position ToDomainPositionRow(string accountId, JsonElement row)
    {
        if (row.ValueKind == JsonValueKind.Object)
        {
            var dto = row.Deserialize<TraderEvolutionPositionDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, "TraderEvolution position payload is empty.");

            if (string.IsNullOrWhiteSpace(dto.AccountId))
            {
                dto = new TraderEvolutionPositionDto
                {
                    AccountId = accountId,
                    Symbol = dto.Symbol,
                    Side = dto.Side,
                    Quantity = dto.Quantity,
                    AveragePrice = dto.AveragePrice,
                    UnrealizedPnl = dto.UnrealizedPnl,
                    EstimatedSwap = dto.EstimatedSwap,
                    UpdatedAt = dto.UpdatedAt
                };
            }

            return ToDomain(dto);
        }

        if (row.ValueKind != JsonValueKind.Array)
        {
            throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, "TraderEvolution position row must be an array or object.");
        }

        var values = row.EnumerateArray().ToArray();
        var sideToken = values.ElementAtOrDefault(2);
        var quantity = ReadDecimal(values.ElementAtOrDefault(3));
        var symbol = ReadString(values.ElementAtOrDefault(10));
        var updatedAt = ReadUnixMilliseconds(values.ElementAtOrDefault(7)) ?? DateTimeOffset.UtcNow;

        return new Position(
            accountId,
            string.IsNullOrWhiteSpace(symbol) ? ReadString(values.ElementAtOrDefault(1)) : symbol,
            ParsePositionSide(sideToken, quantity),
            Math.Abs(quantity),
            ReadDecimal(values.ElementAtOrDefault(4)),
            ReadDecimal(values.ElementAtOrDefault(8)),
            ReadNullableDecimal(values.ElementAtOrDefault(16)),
            updatedAt);
    }

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

    public static AccountState ToDomainAccountState(string accountId, TradingEnv env, JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            var stateElement = payload.TryGetProperty("accountDetails", out var details)
                ? details
                : payload;

            if (stateElement.ValueKind == JsonValueKind.Array)
            {
                return ToDomainAccountState(accountId, env, stateElement);
            }

            var dto = stateElement.Deserialize<TraderEvolutionAccountStateDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, "TraderEvolution account state payload is empty.");

            if (string.IsNullOrWhiteSpace(dto.AccountId))
            {
                dto = new TraderEvolutionAccountStateDto
                {
                    AccountId = accountId,
                    Environment = JsonSerializer.SerializeToElement(env.ToString()),
                    Status = dto.Status,
                    Balance = dto.Balance,
                    Equity = dto.Equity,
                    MarginUsed = dto.MarginUsed,
                    MarginAvailable = dto.MarginAvailable,
                    DailyLoss = dto.DailyLoss,
                    TrailingDrawdown = dto.TrailingDrawdown,
                    UpdatedAt = dto.UpdatedAt
                };
            }

            return ToDomain(dto, env);
        }

        if (payload.ValueKind != JsonValueKind.Array)
        {
            throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, "TraderEvolution account state must be an array or object.");
        }

        var values = payload.EnumerateArray().ToArray();
        var balance = ReadDecimal(values.ElementAtOrDefault(0));
        var projectedBalance = ReadDecimal(values.ElementAtOrDefault(1));
        var availableFunds = ReadDecimal(values.ElementAtOrDefault(2));
        var initialMarginReq = ReadDecimal(values.ElementAtOrDefault(9));
        var maintenanceMarginReq = ReadDecimal(values.ElementAtOrDefault(10));
        var openNetPnl = ReadDecimal(values.ElementAtOrDefault(23));

        return new AccountState(
            accountId,
            env,
            AccountStatus.Active,
            balance,
            projectedBalance == 0 ? balance + openNetPnl : projectedBalance,
            initialMarginReq == 0 ? maintenanceMarginReq : initialMarginReq,
            availableFunds,
            DailyLoss: 0,
            TrailingDrawdown: 0,
            DateTimeOffset.UtcNow);
    }

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

    private static AccountStatus ParseAccountStatus(string value)
    {
        var token = Normalize(value);
        return token switch
        {
            "active" => AccountStatus.Active,
            "suspended" or "blocked" => AccountStatus.Suspended,
            "closed" => AccountStatus.Closed,
            "pending" => AccountStatus.Pending,
            _ => AccountStatus.Active
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

    private static decimal ReadDecimal(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0m;
    }

    private static decimal? ReadNullableDecimal(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return ReadDecimal(value);
    }

    private static string ReadString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty
        };

    private static DateTimeOffset? ReadUnixMilliseconds(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var milliseconds) && milliseconds > 0)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        return null;
    }

    private static void AddJsonMetadata(IDictionary<string, string> metadata, string key, JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return;
        }

        metadata[key] = value.GetRawText();
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
