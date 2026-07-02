using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackTrading.Application;
using StackTrading.Contracts;

namespace StackTrading.Infrastructure.TraderEvolution;

public sealed class TraderEvolutionBrokerExecutionClient : IBrokerExecutionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TraderEvolutionOptions _options;
    private readonly ILogger<TraderEvolutionBrokerExecutionClient> _logger;

    public TraderEvolutionBrokerExecutionClient(
        IHttpClientFactory httpClientFactory,
        IOptions<TraderEvolutionOptions> options,
        ILogger<TraderEvolutionBrokerExecutionClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<BrokerAccount> CreateAccountAsync(ProvisionRequest request, TradingEnv env, CancellationToken ct) =>
        SendAsync<ProvisionRequest, BrokerAccount>(env, HttpMethod.Post, "/api/accounts", request, ct);

    public Task SuspendAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        SendWithoutBodyAsync(env, HttpMethod.Post, $"/api/accounts/{accountId}/suspend?correlationId={Uri.EscapeDataString(correlationId)}", ct);

    public Task CloseAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        SendWithoutBodyAsync(env, HttpMethod.Delete, $"/api/accounts/{accountId}?correlationId={Uri.EscapeDataString(correlationId)}", ct);

    public Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct) =>
        SendAsync<OrderRequest, OrderResult>(request.Environment, HttpMethod.Post, "/api/orders", request, ct);

    public Task<OrderResult> ModifyOrderAsync(string orderId, OrderChange change, CancellationToken ct) =>
        SendAsync<OrderChange, OrderResult>(change.Environment, HttpMethod.Patch, $"/api/orders/{orderId}", change, ct);

    public Task CancelOrderAsync(string orderId, string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        SendWithoutBodyAsync(env, HttpMethod.Delete, $"/api/orders/{orderId}?accountId={Uri.EscapeDataString(accountId)}&correlationId={Uri.EscapeDataString(correlationId)}", ct);

    public Task<IReadOnlyList<Position>> GetPositionsAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        SendForListAsync<Position>(env, $"/api/accounts/{accountId}/positions?correlationId={Uri.EscapeDataString(correlationId)}", ct);

    public Task<AccountState> GetAccountStateAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        SendAsync<AccountState>(env, $"/api/accounts/{accountId}/state?correlationId={Uri.EscapeDataString(correlationId)}", ct);

    public Task TrimToComplianceAsync(string accountId, RiskActionRequest request, CancellationToken ct) =>
        SendWithoutResponseAsync(request.Environment, HttpMethod.Post, $"/api/accounts/{accountId}/risk/trim", request, ct);

    public Task FlattenAllAsync(string accountId, RiskActionRequest request, CancellationToken ct) =>
        SendWithoutResponseAsync(request.Environment, HttpMethod.Post, $"/api/accounts/{accountId}/risk/flatten", request, ct);

    public async IAsyncEnumerable<BrokerEvent> SubscribeAsync(string accountId, TradingEnv env, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<BrokerEvent>();

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await foreach (var brokerEvent in SubscribeOnceAsync(accountId, env, ct))
                    {
                        await channel.Writer.WriteAsync(brokerEvent, ct);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "TraderEvolution stream failed for {AccountId} in {Environment}. Reconnecting.", accountId, env);
                }

                await Task.Delay(TimeSpan.FromSeconds(_options.StreamReconnectDelaySeconds), ct);
            }

            channel.Writer.TryComplete();
        }, ct);

        await foreach (var brokerEvent in channel.Reader.ReadAllAsync(ct))
        {
            yield return brokerEvent;
        }
    }

    private async IAsyncEnumerable<BrokerEvent> SubscribeOnceAsync(string accountId, TradingEnv env, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        var settings = GetEnvironmentOptions(env);
        socket.Options.SetRequestHeader("X-Api-Key", settings.ApiKey);
        socket.Options.SetRequestHeader("X-Api-Secret", settings.ApiSecret);

        var uri = new Uri(new Uri(settings.WebSocketBaseUrl.TrimEnd('/')), $"/ws/accounts/{Uri.EscapeDataString(accountId)}?env={env}");
        await socket.ConnectAsync(uri, ct);

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var message = await ReceiveTextAsync(socket, ct);
            if (string.IsNullOrWhiteSpace(message))
            {
                yield break;
            }

            var brokerEvent = JsonSerializer.Deserialize<BrokerEvent>(message, JsonOptions)
                ?? throw new BrokerAdapterException("TraderEvolution stream returned an empty payload.");

            yield return brokerEvent;
        }
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(TradingEnv env, HttpMethod method, string path, TRequest payload, CancellationToken ct)
    {
        return await ExecuteWithRetryAsync(
            env,
            async cancellationToken =>
            {
                var client = CreateClient(env);
                using var request = new HttpRequestMessage(method, path)
                {
                    Content = JsonContent.Create(payload)
                };

                using var response = await client.SendAsync(request, cancellationToken);
                return await ReadResponseAsync<TResponse>(response, cancellationToken);
            },
            ct);
    }

    private async Task<TResponse> SendAsync<TResponse>(TradingEnv env, string path, CancellationToken ct)
    {
        return await ExecuteWithRetryAsync(
            env,
            async cancellationToken =>
            {
                var client = CreateClient(env);
                using var response = await client.GetAsync(path, cancellationToken);
                return await ReadResponseAsync<TResponse>(response, cancellationToken);
            },
            ct);
    }

    private async Task<IReadOnlyList<TResponse>> SendForListAsync<TResponse>(TradingEnv env, string path, CancellationToken ct)
    {
        var items = await SendAsync<List<TResponse>>(env, path, ct);
        return items;
    }

    private async Task SendWithoutResponseAsync<TRequest>(TradingEnv env, HttpMethod method, string path, TRequest payload, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(
            env,
            async cancellationToken =>
            {
                var client = CreateClient(env);
                using var request = new HttpRequestMessage(method, path)
                {
                    Content = JsonContent.Create(payload)
                };

                using var response = await client.SendAsync(request, cancellationToken);
                await EnsureSuccessAsync(response, cancellationToken);
                return true;
            },
            ct);
    }

    private async Task SendWithoutBodyAsync(TradingEnv env, HttpMethod method, string path, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(
            env,
            async cancellationToken =>
            {
                var client = CreateClient(env);
                using var response = await client.SendAsync(new HttpRequestMessage(method, path), cancellationToken);
                await EnsureSuccessAsync(response, cancellationToken);
                return true;
            },
            ct);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(TradingEnv env, Func<CancellationToken, Task<T>> callback, CancellationToken ct)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= _options.RestRetryCount; attempt++)
        {
            try
            {
                return await callback(ct);
            }
            catch (Exception ex) when (attempt < _options.RestRetryCount && IsTransient(ex))
            {
                lastException = ex;
                _logger.LogWarning(ex, "Transient TraderEvolution REST error in {Environment}. Attempt {Attempt}.", env, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
            catch (Exception ex)
            {
                throw Translate(ex);
            }
        }

        throw Translate(lastException ?? new BrokerAdapterException("TraderEvolution call failed."));
    }

    private HttpClient CreateClient(TradingEnv env)
    {
        var settings = GetEnvironmentOptions(env);
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(settings.ApiBaseUrl.TrimEnd('/'));
        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Remove("X-Api-Secret");
        client.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
        client.DefaultRequestHeaders.Add("X-Api-Secret", settings.ApiSecret);
        return client;
    }

    private TraderEvolutionEnvironmentOptions GetEnvironmentOptions(TradingEnv env)
    {
        var settings = env == TradingEnv.Paper ? _options.Paper : _options.Live;
        if (!settings.Enabled)
        {
            throw new BrokerEnvironmentMismatchException($"{env} environment is disabled.");
        }

        return settings;
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return payload ?? throw new BrokerAdapterException("TraderEvolution returned an empty response body.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new BrokerAdapterException($"TraderEvolution call failed with {(int)response.StatusCode}: {body}");
    }

    private static bool IsTransient(Exception exception) =>
        exception is HttpRequestException || exception is TaskCanceledException;

    private static Exception Translate(Exception exception) =>
        exception is BrokerAdapterException ? exception : new BrokerAdapterException("TraderEvolution transport failed.", exception);

    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var memory = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return string.Empty;
            }

            memory.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }
}
