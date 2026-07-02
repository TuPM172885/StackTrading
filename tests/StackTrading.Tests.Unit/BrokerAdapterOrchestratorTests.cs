using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackTrading.Application;
using StackTrading.Contracts;

namespace StackTrading.Tests.Unit;

public sealed class BrokerAdapterOrchestratorTests
{
    [Fact]
    public async Task PlaceOrderAsync_ShouldGenerateCorrelationId_WhenMissing()
    {
        var executionClient = new StubBrokerExecutionClient();
        var orchestrator = new BrokerAdapterOrchestrator(
            executionClient,
            new NullBrokerEventPublisher(),
            new InMemoryEventDeduplicator(),
            new InMemorySubscriptionRegistry(),
            NullLogger<BrokerAdapterOrchestrator>.Instance);

        await orchestrator.PlaceOrderAsync(
            new OrderRequest(string.Empty, "ACC-1", TradingEnv.Paper, "EURUSD", OrderSide.Buy, OrderType.Market, 1m, null, null, TimeInForce.Day),
            CancellationToken.None);

        executionClient.LastOrderRequest.Should().NotBeNull();
        executionClient.LastOrderRequest!.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SubscribeAsync_ShouldSkipDuplicateEvents()
    {
        var publisher = new RecordingPublisher();
        var executionClient = new StubBrokerExecutionClient
        {
            StreamFactory = CreateDuplicateStream
        };

        var orchestrator = new BrokerAdapterOrchestrator(
            executionClient,
            publisher,
            new InMemoryEventDeduplicator(),
            new InMemorySubscriptionRegistry(),
            NullLogger<BrokerAdapterOrchestrator>.Instance);

        var received = new List<BrokerEvent>();
        await foreach (var brokerEvent in orchestrator.SubscribeAsync("ACC-1", TradingEnv.Paper, CancellationToken.None))
        {
            received.Add(brokerEvent);
        }

        received.Should().HaveCount(1);
        publisher.Events.Should().HaveCount(1);
    }

    private static async IAsyncEnumerable<BrokerEvent> CreateDuplicateStream()
    {
        var payload = JsonSerializer.SerializeToElement(new { orderId = "ORD-1" });
        yield return new BrokerEvent(BrokerEventType.OrderAccepted, "ACC-1", TradingEnv.Paper, "corr-1", "dup-key", DateTimeOffset.UtcNow, payload);
        yield return new BrokerEvent(BrokerEventType.OrderAccepted, "ACC-1", TradingEnv.Paper, "corr-1", "dup-key", DateTimeOffset.UtcNow, payload);
        await Task.CompletedTask;
    }

    private sealed class RecordingPublisher : IBrokerEventPublisher
    {
        public List<BrokerEvent> Events { get; } = [];

        public Task PublishAsync(BrokerEvent brokerEvent, CancellationToken ct)
        {
            Events.Add(brokerEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class StubBrokerExecutionClient : IBrokerExecutionClient
    {
        public OrderRequest? LastOrderRequest { get; private set; }
        public Func<IAsyncEnumerable<BrokerEvent>>? StreamFactory { get; init; }

        public Task<BrokerAccount> CreateAccountAsync(ProvisionRequest request, TradingEnv env, CancellationToken ct) =>
            Task.FromResult(new BrokerAccount("ACC-1", request.ExternalUserId, env, AccountStatus.Active, request.BaseCurrency, request.StartingBalance, request.StartingBalance, DateTimeOffset.UtcNow));

        public Task SuspendAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) => Task.CompletedTask;
        public Task CloseAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) => Task.CompletedTask;

        public Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct)
        {
            LastOrderRequest = request;
            return Task.FromResult(new OrderResult("ORD-1", request.AccountId, request.Environment, OrderStatus.Accepted, request.Symbol, request.Side, request.Quantity, 0m, null, null, DateTimeOffset.UtcNow));
        }

        public Task<OrderResult> ModifyOrderAsync(string orderId, OrderChange change, CancellationToken ct) =>
            Task.FromResult(new OrderResult(orderId, change.AccountId, change.Environment, OrderStatus.Accepted, "EURUSD", OrderSide.Buy, change.Quantity ?? 1m, 0m, null, null, DateTimeOffset.UtcNow));

        public Task CancelOrderAsync(string orderId, string accountId, TradingEnv env, string correlationId, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Position>> GetPositionsAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Position>>([]);
        public Task<AccountState> GetAccountStateAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) => Task.FromResult(new AccountState(accountId, env, AccountStatus.Active, 1m, 1m, 0m, 1m, 0m, 0m, DateTimeOffset.UtcNow));
        public Task TrimToComplianceAsync(string accountId, RiskActionRequest request, CancellationToken ct) => Task.CompletedTask;
        public Task FlattenAllAsync(string accountId, RiskActionRequest request, CancellationToken ct) => Task.CompletedTask;

        public IAsyncEnumerable<BrokerEvent> SubscribeAsync(string accountId, TradingEnv env, CancellationToken ct) =>
            StreamFactory?.Invoke() ?? Empty();

        private static async IAsyncEnumerable<BrokerEvent> Empty()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
