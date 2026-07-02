using Microsoft.AspNetCore.Mvc;
using StackTrading.Application;
using StackTrading.Contracts;

namespace StackTrading.Host.Service.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class BrokerAdapterController : ControllerBase
{
    private readonly IBrokerAdapter _adapter;
    private readonly ISubscriptionRegistry _subscriptionRegistry;

    public BrokerAdapterController(IBrokerAdapter adapter, ISubscriptionRegistry subscriptionRegistry)
    {
        _adapter = adapter;
        _subscriptionRegistry = subscriptionRegistry;
    }

    [HttpPost("accounts")]
    public Task<BrokerAccount> CreateAccountAsync([FromBody] CreateAccountCommand command, CancellationToken ct) =>
        _adapter.CreateAccountAsync(
            new ProvisionRequest(
                command.CorrelationId ?? string.Empty,
                command.ExternalUserId,
                command.ChallengeId,
                command.BaseCurrency,
                command.StartingBalance,
                command.Metadata),
            command.Environment,
            ct);

    [HttpPost("accounts/{accountId}/suspend")]
    public Task SuspendAccountAsync(string accountId, [FromQuery] TradingEnv env, [FromQuery] string? correlationId, CancellationToken ct) =>
        _adapter.SuspendAccountAsync(accountId, env, correlationId ?? string.Empty, ct);

    [HttpDelete("accounts/{accountId}")]
    public Task CloseAccountAsync(string accountId, [FromQuery] TradingEnv env, [FromQuery] string? correlationId, CancellationToken ct) =>
        _adapter.CloseAccountAsync(accountId, env, correlationId ?? string.Empty, ct);

    [HttpPost("orders")]
    public Task<OrderResult> PlaceOrderAsync([FromBody] PlaceOrderCommand command, CancellationToken ct) =>
        _adapter.PlaceOrderAsync(
            new OrderRequest(
                command.CorrelationId ?? string.Empty,
                command.AccountId,
                command.Environment,
                command.Symbol,
                command.Side,
                command.Type,
                command.Quantity,
                command.LimitPrice,
                command.StopPrice,
                command.TimeInForce,
                command.Metadata,
                command.Extensions),
            ct);

    [HttpPatch("orders/{orderId}")]
    public Task<OrderResult> ModifyOrderAsync(string orderId, [FromBody] ModifyOrderCommand command, CancellationToken ct) =>
        _adapter.ModifyOrderAsync(
            orderId,
            new OrderChange(
                command.CorrelationId ?? string.Empty,
                command.AccountId,
                command.Environment,
                command.Quantity,
                command.LimitPrice,
                command.StopPrice,
                command.TimeInForce,
                command.Metadata),
            ct);

    [HttpDelete("orders/{orderId}")]
    public Task CancelOrderAsync(string orderId, [FromQuery] string accountId, [FromQuery] TradingEnv env, [FromQuery] string? correlationId, CancellationToken ct) =>
        _adapter.CancelOrderAsync(orderId, accountId, env, correlationId ?? string.Empty, ct);

    [HttpGet("accounts/{accountId}/positions")]
    public Task<IReadOnlyList<Position>> GetPositionsAsync(string accountId, [FromQuery] TradingEnv env, [FromQuery] string? correlationId, CancellationToken ct) =>
        _adapter.GetPositionsAsync(accountId, env, correlationId ?? string.Empty, ct);

    [HttpGet("accounts/{accountId}/state")]
    public Task<AccountState> GetAccountStateAsync(string accountId, [FromQuery] TradingEnv env, [FromQuery] string? correlationId, CancellationToken ct) =>
        _adapter.GetAccountStateAsync(accountId, env, correlationId ?? string.Empty, ct);

    [HttpPost("accounts/{accountId}/risk/trim")]
    public Task TrimToComplianceAsync(string accountId, [FromBody] RiskCommand command, CancellationToken ct) =>
        _adapter.TrimToComplianceAsync(
            accountId,
            new RiskActionRequest(
                command.CorrelationId ?? string.Empty,
                command.Environment,
                command.Reason,
                command.RequestedBy,
                command.TargetLimit,
                command.Metadata),
            ct);

    [HttpPost("accounts/{accountId}/risk/flatten")]
    public Task FlattenAllAsync(string accountId, [FromBody] RiskCommand command, CancellationToken ct) =>
        _adapter.FlattenAllAsync(
            accountId,
            new RiskActionRequest(
                command.CorrelationId ?? string.Empty,
                command.Environment,
                command.Reason,
                command.RequestedBy,
                command.TargetLimit,
                command.Metadata),
            ct);

    [HttpPost("accounts/{accountId}/subscriptions")]
    public IActionResult StartSubscription(string accountId, [FromQuery] TradingEnv env)
    {
        _subscriptionRegistry.TryRegister(new BrokerSubscription(accountId, env));
        return Accepted();
    }
}

public sealed record CreateAccountCommand(
    TradingEnv Environment,
    string ExternalUserId,
    string ChallengeId,
    string BaseCurrency,
    decimal StartingBalance,
    string? CorrelationId,
    Dictionary<string, string>? Metadata);

public sealed record PlaceOrderCommand(
    string AccountId,
    TradingEnv Environment,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? LimitPrice,
    decimal? StopPrice,
    TimeInForce TimeInForce,
    string? CorrelationId,
    Dictionary<string, string>? Metadata,
    Dictionary<string, string>? Extensions);

public sealed record ModifyOrderCommand(
    string AccountId,
    TradingEnv Environment,
    decimal? Quantity,
    decimal? LimitPrice,
    decimal? StopPrice,
    TimeInForce? TimeInForce,
    string? CorrelationId,
    Dictionary<string, string>? Metadata);

public sealed record RiskCommand(
    TradingEnv Environment,
    string Reason,
    string RequestedBy,
    decimal? TargetLimit,
    string? CorrelationId,
    Dictionary<string, string>? Metadata);
