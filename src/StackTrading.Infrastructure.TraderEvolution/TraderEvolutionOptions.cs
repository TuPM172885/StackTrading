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
    public string ApiBasePath { get; init; } = "/traderevolution/v1";
    public required string ApiKey { get; init; }
    public required string ApiSecret { get; init; }
    public TraderEvolutionAuthMode AuthMode { get; init; } = TraderEvolutionAuthMode.ApiKeyHeaders;
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? Login { get; init; }
    public string? Password { get; init; }
    public string TwoFactorCode { get; init; } = "false";
    public string OAuthTokenPath { get; init; } = "/traderevolution/v1/oauth/token";
    public string PasswordAuthorizePath { get; init; } = "/traderevolution/v1/authorize";
    public int TokenExpirySkewSeconds { get; init; } = 60;
    public int TimeoutSeconds { get; init; } = 10;
    public bool Enabled { get; init; } = true;
}

public enum TraderEvolutionAuthMode
{
    ApiKeyHeaders = 0,
    BearerToken = 1,
    OAuthRefreshToken = 2,
    Password = 3
}

public sealed class PreconfiguredSubscription
{
    public required StackTrading.Contracts.TradingEnv Environment { get; init; }
    public required string AccountId { get; init; }
}
