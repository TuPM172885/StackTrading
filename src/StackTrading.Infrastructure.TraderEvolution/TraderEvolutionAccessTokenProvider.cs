using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using StackTrading.Contracts;

namespace StackTrading.Infrastructure.TraderEvolution;

public interface ITraderEvolutionAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(TradingEnv env, CancellationToken ct);
}

public sealed class TraderEvolutionAccessTokenProvider : ITraderEvolutionAccessTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TraderEvolutionOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<TradingEnv, TokenCacheEntry> _cache = [];

    public TraderEvolutionAccessTokenProvider(IHttpClientFactory httpClientFactory, IOptions<TraderEvolutionOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<string?> GetAccessTokenAsync(TradingEnv env, CancellationToken ct)
    {
        var settings = env == TradingEnv.Paper ? _options.Paper : _options.Live;
        return settings.AuthMode switch
        {
            TraderEvolutionAuthMode.ApiKeyHeaders => null,
            TraderEvolutionAuthMode.BearerToken => Require(settings.AccessToken, "AccessToken"),
            TraderEvolutionAuthMode.OAuthRefreshToken => await GetRefreshTokenGrantTokenAsync(env, settings, ct),
            TraderEvolutionAuthMode.Password => await GetPasswordTokenAsync(env, settings, ct),
            _ => throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, $"Unsupported TraderEvolution auth mode '{settings.AuthMode}'.")
        };
    }

    private async Task<string> GetRefreshTokenGrantTokenAsync(TradingEnv env, TraderEvolutionEnvironmentOptions settings, CancellationToken ct)
    {
        if (TryGetCachedToken(env, out var cachedToken))
        {
            return cachedToken;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (TryGetCachedToken(env, out cachedToken))
            {
                return cachedToken;
            }

            var refreshToken = _cache.TryGetValue(env, out var cached) && !string.IsNullOrWhiteSpace(cached.RefreshToken)
                ? cached.RefreshToken
                : Require(settings.RefreshToken, "RefreshToken");

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_secret"] = Require(settings.ClientSecret, "ClientSecret")
            };

            var token = await RequestTokenAsync(settings, settings.OAuthTokenPath, form, useBasicAuth: true, ct);
            CacheToken(env, token);
            return token.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> GetPasswordTokenAsync(TradingEnv env, TraderEvolutionEnvironmentOptions settings, CancellationToken ct)
    {
        if (TryGetCachedToken(env, out var cachedToken))
        {
            return cachedToken;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (TryGetCachedToken(env, out cachedToken))
            {
                return cachedToken;
            }

            var path = $"{settings.PasswordAuthorizePath}?login={Uri.EscapeDataString(Require(settings.Login, "Login"))}&password={Uri.EscapeDataString(Require(settings.Password, "Password"))}&2faCode={Uri.EscapeDataString(settings.TwoFactorCode)}";
            var token = await RequestTokenAsync(settings, path, form: null, useBasicAuth: false, ct);
            CacheToken(env, token);
            return token.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<TraderEvolutionTokenResponse> RequestTokenAsync(
        TraderEvolutionEnvironmentOptions settings,
        string path,
        IReadOnlyDictionary<string, string>? form,
        bool useBasicAuth,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(settings.ApiBaseUrl.TrimEnd('/'));
        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(form);
        }

        if (useBasicAuth)
        {
            var clientId = Require(settings.ClientId, "ClientId");
            var clientSecret = Require(settings.ClientSecret, "ClientSecret");
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw TraderEvolutionBrokerErrorMapper.ToException(response.StatusCode, response.ReasonPhrase, body);
        }

        var token = await response.Content.ReadFromJsonAsync<TraderEvolutionTokenResponse>(cancellationToken: ct)
            ?? throw new BrokerAdapterException(BrokerErrorCode.AuthenticationFailed, "TraderEvolution token response is empty.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new BrokerAdapterException(BrokerErrorCode.AuthenticationFailed, "TraderEvolution token response is missing access_token.");
        }

        return token;
    }

    private bool TryGetCachedToken(TradingEnv env, out string token)
    {
        var settings = env == TradingEnv.Paper ? _options.Paper : _options.Live;
        var skew = TimeSpan.FromSeconds(Math.Max(0, settings.TokenExpirySkewSeconds));
        if (_cache.TryGetValue(env, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.Add(skew))
        {
            token = cached.AccessToken;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private void CacheToken(TradingEnv env, TraderEvolutionTokenResponse token)
    {
        var expiresIn = token.ExpiresIn <= 0 ? 300 : token.ExpiresIn;
        _cache[env] = new TokenCacheEntry(
            token.AccessToken,
            token.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    private static string Require(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, $"TraderEvolution {fieldName} is required for the configured auth mode.");
        }

        return value;
    }

    private sealed record TokenCacheEntry(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);
}

public sealed class TraderEvolutionTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}
