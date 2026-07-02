using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackTrading.Application;
using StackTrading.Contracts;
using StackTrading.Infrastructure.TraderEvolution;

namespace StackTrading.Host.Service.Infrastructure;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";
    public string BootstrapServers { get; init; } = string.Empty;
    public string Topic { get; init; } = "broker-events-traderevolution";
    public bool Enabled { get; init; }
}

public sealed class KafkaBrokerEventPublisher : IBrokerEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaBrokerEventPublisher> _logger;
    private readonly IProducer<string, string>? _producer;

    public KafkaBrokerEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaBrokerEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.Enabled)
        {
            _producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                EnableIdempotence = true,
                Acks = Acks.All
            }).Build();
        }
    }

    public async Task PublishAsync(BrokerEvent brokerEvent, CancellationToken ct)
    {
        if (!_options.Enabled || _producer is null)
        {
            _logger.LogInformation("Kafka publish skipped because Kafka is disabled.");
            return;
        }

        var message = new Message<string, string>
        {
            Key = brokerEvent.AccountId,
            Value = JsonSerializer.Serialize(brokerEvent, JsonOptions),
            Headers =
            [
                new Header("correlationId", Encoding.UTF8.GetBytes(brokerEvent.CorrelationId)),
                new Header("idempotencyKey", Encoding.UTF8.GetBytes(brokerEvent.IdempotencyKey)),
                new Header("broker", Encoding.UTF8.GetBytes("TraderEvolution")),
                new Header("env", Encoding.UTF8.GetBytes(brokerEvent.Environment.ToString()))
            ]
        };

        await _producer.ProduceAsync(_options.Topic, message, ct);
    }

    public void Dispose() => _producer?.Dispose();
}

public sealed class BrokerRuntimeHealthCheck : IHealthCheck
{
    private readonly IOptions<KafkaOptions> _kafkaOptions;
    private readonly IOptions<TraderEvolutionOptions> _traderEvolutionOptions;

    public BrokerRuntimeHealthCheck(IOptions<KafkaOptions> kafkaOptions, IOptions<TraderEvolutionOptions> traderEvolutionOptions)
    {
        _kafkaOptions = kafkaOptions;
        _traderEvolutionOptions = traderEvolutionOptions;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var kafka = _kafkaOptions.Value;
        var trader = _traderEvolutionOptions.Value;

        if (trader.Paper.Enabled && string.IsNullOrWhiteSpace(trader.Paper.ApiKey))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Paper ApiKey is missing."));
        }

        if (kafka.Enabled && string.IsNullOrWhiteSpace(kafka.BootstrapServers))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka is enabled but BootstrapServers is empty."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Configuration looks valid."));
    }
}
