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

        return ValidateOptionsResult.Success;
    }
}
