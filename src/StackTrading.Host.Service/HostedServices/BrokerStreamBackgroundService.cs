using StackTrading.Application;

namespace StackTrading.Host.Service.HostedServices;

public sealed class BrokerStreamBackgroundService : BackgroundService
{
    private readonly ISubscriptionRegistry _subscriptionRegistry;
    private readonly StackTrading.Contracts.IBrokerAdapter _adapter;
    private readonly ILogger<BrokerStreamBackgroundService> _logger;
    private readonly Dictionary<BrokerSubscription, Task> _activeSubscriptions = [];

    public BrokerStreamBackgroundService(
        ISubscriptionRegistry subscriptionRegistry,
        StackTrading.Contracts.IBrokerAdapter adapter,
        ILogger<BrokerStreamBackgroundService> logger)
    {
        _subscriptionRegistry = subscriptionRegistry;
        _adapter = adapter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var subscription in _subscriptionRegistry.GetAll())
        {
            StartSubscription(subscription, stoppingToken);
        }

        await foreach (var subscription in _subscriptionRegistry.WatchAsync(stoppingToken))
        {
            StartSubscription(subscription, stoppingToken);
        }
    }

    private void StartSubscription(BrokerSubscription subscription, CancellationToken stoppingToken)
    {
        if (_activeSubscriptions.ContainsKey(subscription))
        {
            return;
        }

        _activeSubscriptions[subscription] = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await foreach (var brokerEvent in _adapter.SubscribeAsync(subscription.AccountId, subscription.Environment, stoppingToken))
                    {
                        _logger.LogInformation(
                            "Processed broker event {EventType} for {AccountId} in {Environment}",
                            brokerEvent.EventType,
                            brokerEvent.AccountId,
                            brokerEvent.Environment);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Subscription loop failed for {AccountId} in {Environment}", subscription.AccountId, subscription.Environment);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
        }, stoppingToken);
    }
}
