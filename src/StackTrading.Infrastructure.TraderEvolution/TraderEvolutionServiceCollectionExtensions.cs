using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackTrading.Application;

namespace StackTrading.Infrastructure.TraderEvolution;

public static class TraderEvolutionServiceCollectionExtensions
{
    public static IServiceCollection AddTraderEvolutionInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<TraderEvolutionOptions>()
            .Bind(configuration.GetSection(TraderEvolutionOptions.SectionName))
            .Validate(static options => Uri.IsWellFormedUriString(options.Paper.ApiBaseUrl, UriKind.Absolute), "Paper ApiBaseUrl is invalid.")
            .Validate(static options => Uri.IsWellFormedUriString(options.Paper.WebSocketBaseUrl, UriKind.Absolute), "Paper WebSocketBaseUrl is invalid.")
            .Validate(static options => Uri.IsWellFormedUriString(options.Live.ApiBaseUrl, UriKind.Absolute), "Live ApiBaseUrl is invalid.")
            .Validate(static options => Uri.IsWellFormedUriString(options.Live.WebSocketBaseUrl, UriKind.Absolute), "Live WebSocketBaseUrl is invalid.")
            .Validate(static options => !string.Equals(options.Paper.ApiBaseUrl, options.Live.ApiBaseUrl, StringComparison.OrdinalIgnoreCase), "Paper and Live ApiBaseUrl must be different.")
            .Validate(static options => !string.Equals(options.Paper.WebSocketBaseUrl, options.Live.WebSocketBaseUrl, StringComparison.OrdinalIgnoreCase), "Paper and Live WebSocketBaseUrl must be different.")
            .Validate(static options => !string.Equals(options.Paper.ApiKey, options.Live.ApiKey, StringComparison.Ordinal), "Paper and Live ApiKey must be different.")
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<TraderEvolutionOptions>, TraderEvolutionOptionsValidator>();
        services.AddHttpClient();
        services.AddSingleton<ITraderEvolutionAccessTokenProvider, TraderEvolutionAccessTokenProvider>();
        services.AddSingleton<IBrokerExecutionClient, TraderEvolutionBrokerExecutionClient>();
        return services;
    }
}

public sealed class TraderEvolutionOptionsValidator : IValidateOptions<TraderEvolutionOptions>
{
    public ValidateOptionsResult Validate(string? name, TraderEvolutionOptions options)
    {
        if (!options.Paper.Enabled)
        {
            return ValidateOptionsResult.Fail("Paper environment must be enabled for the initial implementation.");
        }

        var paperResult = ValidateEnvironment(options.Paper, nameof(options.Paper));
        if (paperResult.Failed)
        {
            return paperResult;
        }

        var liveResult = ValidateEnvironment(options.Live, nameof(options.Live));
        if (liveResult.Failed)
        {
            return liveResult;
        }

        return ValidateOptionsResult.Success;
    }

    private static ValidateOptionsResult ValidateEnvironment(TraderEvolutionEnvironmentOptions options, string environmentName)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        return options.AuthMode switch
        {
            TraderEvolutionAuthMode.ApiKeyHeaders when string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiSecret) =>
                ValidateOptionsResult.Fail($"{environmentName} ApiKey and ApiSecret are required for ApiKeyHeaders auth."),
            TraderEvolutionAuthMode.BearerToken when string.IsNullOrWhiteSpace(options.AccessToken) =>
                ValidateOptionsResult.Fail($"{environmentName} AccessToken is required for BearerToken auth."),
            TraderEvolutionAuthMode.OAuthRefreshToken when string.IsNullOrWhiteSpace(options.RefreshToken) || string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret) =>
                ValidateOptionsResult.Fail($"{environmentName} RefreshToken, ClientId and ClientSecret are required for OAuthRefreshToken auth."),
            TraderEvolutionAuthMode.Password when string.IsNullOrWhiteSpace(options.Login) || string.IsNullOrWhiteSpace(options.Password) =>
                ValidateOptionsResult.Fail($"{environmentName} Login and Password are required for Password auth."),
            _ => ValidateOptionsResult.Success
        };
    }
}
