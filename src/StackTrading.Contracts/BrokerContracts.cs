using System.Text.Json;

namespace StackTrading.Contracts;

public enum TradingEnv
{
    Paper = 0,
    Live = 1
}

public enum AccountStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Closed = 3
}

public enum OrderSide
{
    Buy = 0,
    Sell = 1
}

public enum OrderType
{
    Market = 0,
    Limit = 1,
    Stop = 2
}

public enum TimeInForce
{
    Day = 0,
    Gtc = 1,
    Ioc = 2,
    Fok = 3
}

public enum OrderStatus
{
    Pending = 0,
    Accepted = 1,
    PartiallyFilled = 2,
    Filled = 3,
    Cancelled = 4,
    Rejected = 5
}

public enum PositionSide
{
    Long = 0,
    Short = 1,
    Flat = 2
}

public enum BrokerEventType
{
    OrderAccepted = 0,
    OrderFilled = 1,
    OrderCancelled = 2,
    OrderRejected = 3,
    PositionUpdated = 4,
    AccountStateChanged = 5,
    ExecutionReport = 6,
    MarginBreach = 7,
    DrawdownBreach = 8,
    LiquidationExecuted = 9
}

public enum BrokerErrorCode
{
    Unknown = 0,
    AuthenticationFailed = 1,
    AuthorizationFailed = 2,
    ValidationFailed = 3,
    NotFound = 4,
    RateLimited = 5,
    Timeout = 6,
    BrokerUnavailable = 7,
    EnvironmentMismatch = 8,
    DuplicateRequest = 9
}

public sealed record ProvisionRequest(
    string CorrelationId,
    string ExternalUserId,
    string ChallengeId,
    string BaseCurrency,
    decimal StartingBalance,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record BrokerAccount(
    string AccountId,
    string ExternalUserId,
    TradingEnv Environment,
    AccountStatus Status,
    string BaseCurrency,
    decimal Balance,
    decimal Equity,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record OrderRequest(
    string CorrelationId,
    string AccountId,
    TradingEnv Environment,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? LimitPrice,
    decimal? StopPrice,
    TimeInForce TimeInForce,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyDictionary<string, string>? Extensions = null);

public sealed record OrderChange(
    string CorrelationId,
    string AccountId,
    TradingEnv Environment,
    decimal? Quantity = null,
    decimal? LimitPrice = null,
    decimal? StopPrice = null,
    TimeInForce? TimeInForce = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record OrderResult(
    string OrderId,
    string AccountId,
    TradingEnv Environment,
    OrderStatus Status,
    string Symbol,
    OrderSide Side,
    decimal Quantity,
    decimal FilledQuantity,
    decimal? AverageFillPrice,
    string? BrokerMessage,
    DateTimeOffset OccurredAt);

public sealed record Position(
    string AccountId,
    string Symbol,
    PositionSide Side,
    decimal Quantity,
    decimal AveragePrice,
    decimal UnrealizedPnl,
    decimal? EstimatedSwap,
    DateTimeOffset UpdatedAt);

public sealed record AccountState(
    string AccountId,
    TradingEnv Environment,
    AccountStatus Status,
    decimal Balance,
    decimal Equity,
    decimal MarginUsed,
    decimal MarginAvailable,
    decimal DailyLoss,
    decimal TrailingDrawdown,
    DateTimeOffset UpdatedAt);

public sealed record RiskActionRequest(
    string CorrelationId,
    TradingEnv Environment,
    string Reason,
    string RequestedBy,
    decimal? TargetLimit = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record BrokerEvent(
    BrokerEventType EventType,
    string AccountId,
    TradingEnv Environment,
    string CorrelationId,
    string IdempotencyKey,
    DateTimeOffset OccurredAt,
    JsonElement Payload);

public interface IBrokerAdapter
{
    Task<BrokerAccount> CreateAccountAsync(ProvisionRequest req, TradingEnv env, CancellationToken ct);
    Task SuspendAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct);
    Task CloseAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct);
    Task<OrderResult> PlaceOrderAsync(OrderRequest req, CancellationToken ct);
    Task<OrderResult> ModifyOrderAsync(string orderId, OrderChange change, CancellationToken ct);
    Task CancelOrderAsync(string orderId, string accountId, TradingEnv env, string correlationId, CancellationToken ct);
    Task<IReadOnlyList<Position>> GetPositionsAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct);
    Task<AccountState> GetAccountStateAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct);
    Task TrimToComplianceAsync(string accountId, RiskActionRequest request, CancellationToken ct);
    Task FlattenAllAsync(string accountId, RiskActionRequest request, CancellationToken ct);
    IAsyncEnumerable<BrokerEvent> SubscribeAsync(string accountId, TradingEnv env, CancellationToken ct);
}

public class BrokerAdapterException : Exception
{
    public BrokerAdapterException(string message) : this(BrokerErrorCode.Unknown, message)
    {
    }

    public BrokerAdapterException(string message, Exception innerException) : this(BrokerErrorCode.Unknown, message, innerException)
    {
    }

    public BrokerAdapterException(BrokerErrorCode code, string message) : base(message)
    {
        Code = code;
    }

    public BrokerAdapterException(BrokerErrorCode code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }

    public BrokerErrorCode Code { get; }
}

public sealed class BrokerEnvironmentMismatchException : BrokerAdapterException
{
    public BrokerEnvironmentMismatchException(string message) : base(BrokerErrorCode.EnvironmentMismatch, message)
    {
    }
}
