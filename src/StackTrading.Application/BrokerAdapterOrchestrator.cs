using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using StackTrading.Contracts;

namespace StackTrading.Application;

public interface IBrokerExecutionClient
{
    Task<BrokerAccount> CreateAccountAsync(ProvisionRequest request, TradingEnv env, CancellationToken ct);
    Task SuspendAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct);
    Task CloseAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct);
    Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct);
    Task<OrderResult> ModifyOrderAsync(string orderId, OrderChange change, CancellationToken ct);
    Task CancelOrderAsync(string orderId, string accountId, TradingEnv env, string correlationId, CancellationToken ct);
    Task<IReadOnlyList<Position>> GetPositionsAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct);
    Task<AccountState> GetAccountStateAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct);
    Task TrimToComplianceAsync(string accountId, RiskActionRequest request, CancellationToken ct);
    Task FlattenAllAsync(string accountId, RiskActionRequest request, CancellationToken ct);
    IAsyncEnumerable<BrokerEvent> SubscribeAsync(string accountId, TradingEnv env, CancellationToken ct);
}

public interface IBrokerEventPublisher
{
    Task PublishAsync(BrokerEvent brokerEvent, CancellationToken ct);
}

public interface IEventDeduplicator
{
    bool TryMarkProcessed(string idempotencyKey, DateTimeOffset occurredAt);
}

public interface ISubscriptionRegistry
{
    IReadOnlyCollection<BrokerSubscription> GetAll();
    bool TryRegister(BrokerSubscription subscription);
    IAsyncEnumerable<BrokerSubscription> WatchAsync(CancellationToken cancellationToken);
}

public sealed record BrokerSubscription(string AccountId, TradingEnv Environment);

public sealed class InMemoryEventDeduplicator : IEventDeduplicator
{
    private readonly TimeSpan _ttl;
    private readonly object _lock = new();
    private readonly Dictionary<string, DateTimeOffset> _processed = new(StringComparer.Ordinal);

    public InMemoryEventDeduplicator(TimeSpan? ttl = null)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(15);
    }

    public bool TryMarkProcessed(string idempotencyKey, DateTimeOffset occurredAt)
    {
        lock (_lock)
        {
            var expiredKeys = _processed
                .Where(pair => occurredAt - pair.Value > _ttl)
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var expiredKey in expiredKeys)
            {
                _processed.Remove(expiredKey);
            }

            if (_processed.ContainsKey(idempotencyKey))
            {
                return false;
            }

            _processed[idempotencyKey] = occurredAt;
            return true;
        }
    }
}

public sealed class InMemorySubscriptionRegistry : ISubscriptionRegistry
{
    private readonly object _lock = new();
    private readonly HashSet<BrokerSubscription> _subscriptions = [];
    private readonly List<ChannelWriter<BrokerSubscription>> _watchers = [];

    public IReadOnlyCollection<BrokerSubscription> GetAll()
    {
        lock (_lock)
        {
            return _subscriptions.ToArray();
        }
    }

    public bool TryRegister(BrokerSubscription subscription)
    {
        List<ChannelWriter<BrokerSubscription>> watchers;

        lock (_lock)
        {
            if (!_subscriptions.Add(subscription))
            {
                return false;
            }

            watchers = _watchers.ToList();
        }

        foreach (var watcher in watchers)
        {
            watcher.TryWrite(subscription);
        }

        return true;
    }

    public async IAsyncEnumerable<BrokerSubscription> WatchAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<BrokerSubscription>();

        lock (_lock)
        {
            _watchers.Add(channel.Writer);
        }

        try
        {
            await foreach (var subscription in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return subscription;
            }
        }
        finally
        {
            lock (_lock)
            {
                _watchers.Remove(channel.Writer);
            }
        }
    }
}

public sealed class NullBrokerEventPublisher : IBrokerEventPublisher
{
    public Task PublishAsync(BrokerEvent brokerEvent, CancellationToken ct) => Task.CompletedTask;
}

public sealed class BrokerAdapterOrchestrator : IBrokerAdapter
{
    private readonly IBrokerExecutionClient _client;
    private readonly IBrokerEventPublisher _publisher;
    private readonly IEventDeduplicator _deduplicator;
    private readonly ISubscriptionRegistry _subscriptionRegistry;
    private readonly ILogger<BrokerAdapterOrchestrator> _logger;

    public BrokerAdapterOrchestrator(
        IBrokerExecutionClient client,
        IBrokerEventPublisher publisher,
        IEventDeduplicator deduplicator,
        ISubscriptionRegistry subscriptionRegistry,
        ILogger<BrokerAdapterOrchestrator> logger)
    {
        _client = client;
        _publisher = publisher;
        _deduplicator = deduplicator;
        _subscriptionRegistry = subscriptionRegistry;
        _logger = logger;
    }

    public async Task<BrokerAccount> CreateAccountAsync(ProvisionRequest req, TradingEnv env, CancellationToken ct)
    {
        var account = await _client.CreateAccountAsync(WithCorrelation(req), env, ct);
        _subscriptionRegistry.TryRegister(new BrokerSubscription(account.AccountId, env));
        return account;
    }

    public Task SuspendAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        _client.SuspendAccountAsync(accountId, env, EnsureCorrelationId(correlationId), ct);

    public Task CloseAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        _client.CloseAccountAsync(accountId, env, EnsureCorrelationId(correlationId), ct);

    public Task<OrderResult> PlaceOrderAsync(OrderRequest req, CancellationToken ct) =>
        _client.PlaceOrderAsync(WithCorrelation(req), ct);

    public Task<OrderResult> ModifyOrderAsync(string orderId, OrderChange change, CancellationToken ct) =>
        _client.ModifyOrderAsync(orderId, WithCorrelation(change), ct);

    public Task CancelOrderAsync(string orderId, string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        _client.CancelOrderAsync(orderId, accountId, env, EnsureCorrelationId(correlationId), ct);

    public Task<IReadOnlyList<Position>> GetPositionsAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        _client.GetPositionsAsync(accountId, env, EnsureCorrelationId(correlationId), ct);

    public Task<AccountState> GetAccountStateAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        _client.GetAccountStateAsync(accountId, env, EnsureCorrelationId(correlationId), ct);

    public Task TrimToComplianceAsync(string accountId, RiskActionRequest request, CancellationToken ct) =>
        _client.TrimToComplianceAsync(accountId, WithCorrelation(request), ct);

    public Task FlattenAllAsync(string accountId, RiskActionRequest request, CancellationToken ct) =>
        _client.FlattenAllAsync(accountId, WithCorrelation(request), ct);

    public async IAsyncEnumerable<BrokerEvent> SubscribeAsync(string accountId, TradingEnv env, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var brokerEvent in _client.SubscribeAsync(accountId, env, ct))
        {
            if (!_deduplicator.TryMarkProcessed(brokerEvent.IdempotencyKey, brokerEvent.OccurredAt))
            {
                _logger.LogDebug("Skipped duplicate broker event {IdempotencyKey}", brokerEvent.IdempotencyKey);
                continue;
            }

            await _publisher.PublishAsync(brokerEvent, ct);
            yield return brokerEvent;
        }
    }

    private static ProvisionRequest WithCorrelation(ProvisionRequest request) =>
        request with { CorrelationId = EnsureCorrelationId(request.CorrelationId) };

    private static OrderRequest WithCorrelation(OrderRequest request) =>
        request with { CorrelationId = EnsureCorrelationId(request.CorrelationId) };

    private static OrderChange WithCorrelation(OrderChange change) =>
        change with { CorrelationId = EnsureCorrelationId(change.CorrelationId) };

    private static RiskActionRequest WithCorrelation(RiskActionRequest request) =>
        request with { CorrelationId = EnsureCorrelationId(request.CorrelationId) };

    private static string EnsureCorrelationId(string? correlationId) =>
        string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId;
}
