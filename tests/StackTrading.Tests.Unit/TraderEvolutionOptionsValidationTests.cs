using FluentAssertions;
using Microsoft.Extensions.Options;
using StackTrading.Infrastructure.TraderEvolution;

namespace StackTrading.Tests.Unit;

public sealed class TraderEvolutionOptionsValidationTests
{
    [Fact]
    public void Validate_ShouldRejectDisabledPaperEnvironment()
    {
        var validator = new TraderEvolutionOptionsValidator();
        var options = CreateOptions(CreateEnvironment(enabled: false));

        var result = validator.Validate(Options.DefaultName, options);
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldRejectBearerTokenAuth_WhenAccessTokenIsMissing()
    {
        var validator = new TraderEvolutionOptionsValidator();
        var options = CreateOptions(CreateEnvironment(authMode: TraderEvolutionAuthMode.BearerToken, accessToken: null));

        var result = validator.Validate(Options.DefaultName, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains("AccessToken", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ShouldAcceptOAuthRefreshTokenAuth_WhenRequiredFieldsExist()
    {
        var validator = new TraderEvolutionOptionsValidator();
        var options = CreateOptions(
            CreateEnvironment(
                authMode: TraderEvolutionAuthMode.OAuthRefreshToken,
                refreshToken: "refresh-token",
                clientId: "publictest",
                clientSecret: "client-secret"));

        var result = validator.Validate(Options.DefaultName, options);

        result.Failed.Should().BeFalse();
    }

    [Fact]
    public async Task AccessTokenProvider_ShouldReturnConfiguredBearerToken()
    {
        var options = CreateOptions(CreateEnvironment(authMode: TraderEvolutionAuthMode.BearerToken, accessToken: "paper-token"));
        var provider = new TraderEvolutionAccessTokenProvider(new ThrowingHttpClientFactory(), Options.Create(options));

        var token = await provider.GetAccessTokenAsync(StackTrading.Contracts.TradingEnv.Paper, CancellationToken.None);

        token.Should().Be("paper-token");
    }

    private static TraderEvolutionOptions CreateOptions(TraderEvolutionEnvironmentOptions? paper = null) =>
        new()
        {
            Paper = paper ?? CreateEnvironment(),
            Live = CreateEnvironment(apiBaseUrl: "http://live.local", webSocketBaseUrl: "ws://live.local", apiKey: "live-key", apiSecret: "live-secret")
        };

    private static TraderEvolutionEnvironmentOptions CreateEnvironment(
        string apiBaseUrl = "http://paper.local",
        string webSocketBaseUrl = "ws://paper.local",
        string apiKey = "paper-key",
        string apiSecret = "paper-secret",
        bool enabled = true,
        TraderEvolutionAuthMode authMode = TraderEvolutionAuthMode.ApiKeyHeaders,
        string? accessToken = "access-token",
        string? refreshToken = null,
        string? clientId = null,
        string? clientSecret = null) =>
        new()
        {
            ApiBaseUrl = apiBaseUrl,
            WebSocketBaseUrl = webSocketBaseUrl,
            ApiKey = apiKey,
            ApiSecret = apiSecret,
            Enabled = enabled,
            AuthMode = authMode,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ClientId = clientId,
            ClientSecret = clientSecret
        };

    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException("This test should not request a token over HTTP.");
    }
}
