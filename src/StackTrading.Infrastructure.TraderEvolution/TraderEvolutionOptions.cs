namespace StackTrading.Infrastructure.TraderEvolution;

public sealed class TraderEvolutionOptions
{
    public const string SectionName = "TraderEvolution";
    public required TraderEvolutionEnvironmentOptions Paper { get; init; }
    public required TraderEvolutionEnvironmentOptions Live { get; init; }
    public int RestRetryCount { get; init; } = 3;
    public int StreamReconnectDelaySeconds { get; init; } = 3;
    public List<PreconfiguredSubscription> PreconfiguredSubscriptions { get; init; } = [];
}

public sealed class TraderEvolutionEnvironmentOptions
{
    public required string ApiBaseUrl { get; init; }
    public required string WebSocketBaseUrl { get; init; }
    public required string ApiKey { get; init; }
    public required string ApiSecret { get; init; }
    public int TimeoutSeconds { get; init; } = 10;
    public bool Enabled { get; init; } = true;
}

public sealed class PreconfiguredSubscription
{
    public required StackTrading.Contracts.TradingEnv Environment { get; init; }
    public required string AccountId { get; init; }
}
