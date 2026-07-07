using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Net.Http.Headers;
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
    private static readonly string[] TradeEventSubscriptions =
    [
        "orders",
        "openPositions",
        "closePositions",
        "executions",
        "riskRules",
        "marginWarning",
        "stopOut"
    ];
    private static readonly string[] AccountEventSubscriptions =
    [
        "accountDetailsData",
        "account"
    ];
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITraderEvolutionAccessTokenProvider _accessTokenProvider;
    private readonly TraderEvolutionOptions _options;
    private readonly ILogger<TraderEvolutionBrokerExecutionClient> _logger;
    private readonly Dictionary<string, string> _instrumentIdCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _instrumentCacheLock = new(1, 1);

    public TraderEvolutionBrokerExecutionClient(
        IHttpClientFactory httpClientFactory,
        ITraderEvolutionAccessTokenProvider accessTokenProvider,
        IOptions<TraderEvolutionOptions> options,
        ILogger<TraderEvolutionBrokerExecutionClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _accessTokenProvider = accessTokenProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BrokerAccount> CreateAccountAsync(ProvisionRequest request, TradingEnv env, CancellationToken ct)
    {
        var accounts = await SendAsync<TraderEvolutionAccountsDataDto>(env, ApiPath(env, "accounts"), ct);
        var account = accounts.Accounts.FirstOrDefault()
            ?? throw new BrokerAdapterException(BrokerErrorCode.NotFound, "TraderEvolution returned no account for the authenticated user.");

        return TraderEvolutionMapper.ToDomain(account, request, env);
    }

    public Task SuspendAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        SendWithoutBodyAsync(env, HttpMethod.Post, ApiPath(env, $"accounts/{Uri.EscapeDataString(accountId)}/suspend?correlationId={Uri.EscapeDataString(correlationId)}"), ct);

    public Task CloseAccountAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        SendWithoutBodyAsync(env, HttpMethod.Delete, ApiPath(env, $"accounts/{Uri.EscapeDataString(accountId)}?correlationId={Uri.EscapeDataString(correlationId)}"), ct);

    public async Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct)
    {
        var tradableInstrumentId = await ResolveTradableInstrumentIdAsync(request.AccountId, request.Environment, request.Symbol, ct);
        var payload = ToTraderEvolutionOrderRequest(request, tradableInstrumentId);
        var order = await SendAsync<TraderEvolutionOrderRequestDto, TraderEvolutionOrderDto>(request.Environment, HttpMethod.Post, ApiPath(request.Environment, $"accounts/{Uri.EscapeDataString(request.AccountId)}/orders"), payload, ct);
        return TraderEvolutionMapper.ToDomain(order, request.Environment);
    }

    public async Task<OrderResult> ModifyOrderAsync(string orderId, OrderChange change, CancellationToken ct)
    {
        var payload = new TraderEvolutionOrderChangeDto
        {
            Qty = change.Quantity,
            Price = change.LimitPrice,
            StopPrice = change.StopPrice,
            Validity = change.TimeInForce?.ToString(),
            ClOrderId = change.CorrelationId
        };

        var order = await SendAsync<TraderEvolutionOrderChangeDto, TraderEvolutionOrderDto>(change.Environment, HttpMethod.Patch, ApiPath(change.Environment, $"accounts/{Uri.EscapeDataString(change.AccountId)}/orders/{Uri.EscapeDataString(orderId)}"), payload, ct);
        return TraderEvolutionMapper.ToDomain(order, change.Environment);
    }

    public Task CancelOrderAsync(string orderId, string accountId, TradingEnv env, string correlationId, CancellationToken ct) =>
        SendWithoutBodyAsync(env, HttpMethod.Delete, ApiPath(env, $"accounts/{Uri.EscapeDataString(accountId)}/orders/{Uri.EscapeDataString(orderId)}?correlationId={Uri.EscapeDataString(correlationId)}"), ct);

    public async Task<IReadOnlyList<Position>> GetPositionsAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct)
    {
        var positions = await SendAsync<TraderEvolutionPositionsDataDto>(env, ApiPath(env, $"accounts/{Uri.EscapeDataString(accountId)}/positions?correlationId={Uri.EscapeDataString(correlationId)}"), ct);
        return positions.Positions.Select(row => TraderEvolutionMapper.ToDomainPositionRow(accountId, row)).ToList();
    }

    public async Task<AccountState> GetAccountStateAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct)
    {
        var state = await SendAsync<JsonElement>(env, ApiPath(env, $"accounts/{Uri.EscapeDataString(accountId)}/state?correlationId={Uri.EscapeDataString(correlationId)}"), ct);
        return TraderEvolutionMapper.ToDomainAccountState(accountId, env, state);
    }

    public async Task TrimToComplianceAsync(string accountId, RiskActionRequest request, CancellationToken ct)
    {
        var positions = await GetPositionsAsync(accountId, request.Environment, request.CorrelationId, ct);
        var orders = TraderEvolutionRiskOrderPlanner.PlanTrimOrders(accountId, request, positions);
        await ExecuteRiskOrdersAsync("TrimToCompliance", orders, request, ct);
    }

    public async Task FlattenAllAsync(string accountId, RiskActionRequest request, CancellationToken ct)
    {
        var activeOrders = await GetActiveOrdersAsync(accountId, request.Environment, request.CorrelationId, ct);
        foreach (var order in activeOrders.Where(IsCancellableOrder))
        {
            _logger.LogInformation(
                "Cancelling active order {OrderId} before FlattenAll for {AccountId} in {Environment}. CorrelationId={CorrelationId}",
                order.OrderId,
                accountId,
                request.Environment,
                request.CorrelationId);

            await CancelOrderAsync(order.OrderId, accountId, request.Environment, request.CorrelationId, ct);
        }

        var positions = await GetPositionsAsync(accountId, request.Environment, request.CorrelationId, ct);
        var orders = TraderEvolutionRiskOrderPlanner.PlanFlattenOrders(accountId, request, positions);
        await ExecuteRiskOrdersAsync("FlattenAll", orders, request, ct);
    }

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
        var channel = Channel.CreateUnbounded<BrokerEvent>();
        var tradeTask = RunStreamAsync(accountId, env, "tradeEvents", TradeEventSubscriptions, channel.Writer, ct);
        var accountTask = RunStreamAsync(accountId, env, "accounts", AccountEventSubscriptions, channel.Writer, ct);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(tradeTask, accountTask);
                channel.Writer.TryComplete();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, ct);

        await foreach (var brokerEvent in channel.Reader.ReadAllAsync(ct))
        {
            yield return brokerEvent;
        }
    }

    private async Task RunStreamAsync(
        string accountId,
        TradingEnv env,
        string streamName,
        IReadOnlyCollection<string> subscriptionTypes,
        ChannelWriter<BrokerEvent> writer,
        CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        var settings = GetEnvironmentOptions(env);
        await ApplyWebSocketAuthAsync(socket, env, settings, ct);

        var uri = new Uri(new Uri(settings.WebSocketBaseUrl.TrimEnd('/')), $"{NormalizePath(settings.ApiBasePath)}/stream/{streamName}");
        await socket.ConnectAsync(uri, ct);
        await SubscribeStreamAsync(socket, accountId, subscriptionTypes, ct);

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var message = await ReceiveTextAsync(socket, ct);
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            foreach (var brokerEvent in await MapStreamMessageAsync(socket, accountId, env, message, ct))
            {
                await writer.WriteAsync(brokerEvent, ct);
            }
        }
    }

    private static async Task SubscribeStreamAsync(ClientWebSocket socket, string accountId, IReadOnlyCollection<string> subscriptionTypes, CancellationToken ct)
    {
        var requestId = 1;
        foreach (var subscriptionType in subscriptionTypes)
        {
            var subscription = new TraderEvolutionStreamSubscriptionDto
            {
                Event = "subscribe",
                RequestId = requestId++,
                Payload = new TraderEvolutionStreamSubscriptionPayloadDto
                {
                    AccountId = accountId,
                    St = subscriptionType
                }
            };

            var payload = JsonSerializer.Serialize(subscription, JsonOptions);
            await SendTextAsync(socket, payload, ct);
        }
    }

    private static async Task<IReadOnlyList<BrokerEvent>> MapStreamMessageAsync(
        ClientWebSocket socket,
        string fallbackAccountId,
        TradingEnv env,
        string message,
        CancellationToken ct)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        if (root.TryGetProperty("event", out var eventElement)
            && string.Equals(eventElement.GetString(), "PING", StringComparison.OrdinalIgnoreCase))
        {
            var pong = JsonSerializer.Serialize(new
            {
                @event = "PONG",
                t = root.TryGetProperty("t", out var timestamp) ? timestamp.Clone() : JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            }, JsonOptions);

            await SendTextAsync(socket, pong, ct);
            return [];
        }

        if (root.TryGetProperty("eventType", out _) || root.TryGetProperty("EventType", out _))
        {
            var dto = root.Deserialize<TraderEvolutionBrokerEventDto>(JsonOptions)
                ?? throw new BrokerAdapterException("TraderEvolution stream returned an empty broker event payload.");

            return [TraderEvolutionMapper.ToDomain(dto, env)];
        }

        if (root.TryGetProperty("s", out var statusElement)
            && string.Equals(statusElement.GetString(), "error", StringComparison.OrdinalIgnoreCase))
        {
            var messageText = root.TryGetProperty("errmsg", out var errorElement)
                ? errorElement.GetString()
                : "TraderEvolution stream returned an error response.";
            throw new BrokerAdapterException(BrokerErrorCode.BrokerUnavailable, messageText ?? "TraderEvolution stream returned an error response.");
        }

        if (root.TryGetProperty("event", out var ackEventElement)
            && string.Equals(ackEventElement.GetString(), "subscribe", StringComparison.OrdinalIgnoreCase))
        {
            if (root.TryGetProperty("s", out var ackStatus)
                && string.Equals(ackStatus.GetString(), "error", StringComparison.OrdinalIgnoreCase))
            {
                var ackError = root.TryGetProperty("errmsg", out var ackErrMsg)
                    ? ackErrMsg.GetString()
                    : "TraderEvolution subscribe request was rejected.";
                throw new BrokerAdapterException(BrokerErrorCode.BrokerUnavailable, ackError ?? "TraderEvolution subscribe request was rejected.");
            }

            return [];
        }

        if (!root.TryGetProperty("st", out var stElement))
        {
            return [];
        }

        var st = stElement.GetString() ?? string.Empty;
        var accountId = root.TryGetProperty("accountId", out var accountElement)
            ? accountElement.ToString()
            : fallbackAccountId;
        var payload = root.TryGetProperty("d", out var dataElement)
            ? dataElement.Clone()
            : JsonSerializer.SerializeToElement(new { });

        return st switch
        {
            "orders" => MapOrderStreamEvents(accountId, env, payload),
            "openPositions" or "closePositions" => [CreateStreamEvent(BrokerEventType.PositionUpdated, accountId, env, st, payload)],
            "executions" => [CreateStreamEvent(BrokerEventType.ExecutionReport, accountId, env, st, payload)],
            "riskRules" => [CreateStreamEvent(BrokerEventType.DrawdownBreach, accountId, env, st, payload)],
            "marginWarning" => [CreateStreamEvent(BrokerEventType.MarginBreach, accountId, env, st, payload)],
            "stopOut" => [CreateStreamEvent(BrokerEventType.LiquidationExecuted, accountId, env, st, payload)],
            "accountDetailsData" or "account" => [CreateStreamEvent(BrokerEventType.AccountStateChanged, accountId, env, st, payload)],
            _ => []
        };
    }

    private static IReadOnlyList<BrokerEvent> MapOrderStreamEvents(string accountId, TradingEnv env, JsonElement payload)
    {
        if (!payload.TryGetProperty("orders", out var ordersElement) || ordersElement.ValueKind != JsonValueKind.Array)
        {
            return [CreateStreamEvent(BrokerEventType.OrderAccepted, accountId, env, "orders", payload)];
        }

        return ordersElement
            .EnumerateArray()
            .Select(row =>
            {
                var order = TraderEvolutionMapper.ToDomainOrderRow(accountId, env, row);
                var eventType = order.Status switch
                {
                    OrderStatus.Filled or OrderStatus.PartiallyFilled => BrokerEventType.OrderFilled,
                    OrderStatus.Cancelled => BrokerEventType.OrderCancelled,
                    OrderStatus.Rejected => BrokerEventType.OrderRejected,
                    _ => BrokerEventType.OrderAccepted
                };

                return new BrokerEvent(
                    eventType,
                    accountId,
                    env,
                    $"stream-{accountId}-{order.OrderId}",
                    $"traderevolution:{env}:{accountId}:orders:{order.OrderId}:{order.Status}:{order.OccurredAt.ToUnixTimeMilliseconds()}",
                    order.OccurredAt,
                    JsonSerializer.SerializeToElement(order, JsonOptions));
            })
            .ToList();
    }

    private static BrokerEvent CreateStreamEvent(BrokerEventType eventType, string accountId, TradingEnv env, string streamType, JsonElement payload)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        return new BrokerEvent(
            eventType,
            accountId,
            env,
            $"stream-{accountId}-{streamType}",
            $"traderevolution:{env}:{accountId}:{streamType}:{occurredAt.ToUnixTimeMilliseconds()}",
            occurredAt,
            payload);
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
                await ApplyRequestAuthAsync(request, env, ct);

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
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                await ApplyRequestAuthAsync(request, env, ct);

                using var response = await client.SendAsync(request, cancellationToken);
                return await ReadResponseAsync<TResponse>(response, cancellationToken);
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
                using var request = new HttpRequestMessage(method, path);
                await ApplyRequestAuthAsync(request, env, ct);

                using var response = await client.SendAsync(request, cancellationToken);
                await EnsureSuccessAsync(response, cancellationToken);
                return true;
            },
            ct);
    }

    private async Task ExecuteRiskOrdersAsync(
        string actionName,
        IReadOnlyCollection<OrderRequest> orders,
        RiskActionRequest request,
        CancellationToken ct)
    {
        if (orders.Count == 0)
        {
            _logger.LogInformation(
                "{RiskAction} produced no reduce orders in {Environment}. Reason={Reason}; RequestedBy={RequestedBy}; CorrelationId={CorrelationId}",
                actionName,
                request.Environment,
                request.Reason,
                request.RequestedBy,
                request.CorrelationId);
            return;
        }

        foreach (var order in orders)
        {
            _logger.LogInformation(
                "Executing {RiskAction} reduce order for {AccountId} {Symbol} {Side} {Quantity} in {Environment}. Reason={Reason}; RequestedBy={RequestedBy}; CorrelationId={CorrelationId}",
                actionName,
                order.AccountId,
                order.Symbol,
                order.Side,
                order.Quantity,
                order.Environment,
                request.Reason,
                request.RequestedBy,
                request.CorrelationId);

            await PlaceOrderAsync(order, ct);
        }
    }

    private async Task<string> ResolveTradableInstrumentIdAsync(string accountId, TradingEnv env, string symbol, CancellationToken ct)
    {
        var cacheKey = $"{env}:{accountId}:{symbol}";
        await _instrumentCacheLock.WaitAsync(ct);
        try
        {
            if (_instrumentIdCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var instruments = await SendAsync<TraderEvolutionInstrumentsDataDto>(
                env,
                ApiPath(env, $"accounts/{Uri.EscapeDataString(accountId)}/instruments"),
                ct);

            var match = instruments.Instruments.FirstOrDefault(instrument =>
                string.Equals(instrument.Symbol, symbol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(instrument.Name, symbol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(instrument.Ticker, symbol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(instrument.Id, symbol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(instrument.TradableInstrumentId, symbol, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, $"TraderEvolution symbol '{symbol}' is not available for account '{accountId}'.");
            }

            var id = FirstNonEmpty(match.TradableInstrumentId, match.Id);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new BrokerAdapterException(BrokerErrorCode.ValidationFailed, $"TraderEvolution instrument '{symbol}' is missing tradableInstrumentId.");
            }

            _instrumentIdCache[cacheKey] = id;
            return id;
        }
        finally
        {
            _instrumentCacheLock.Release();
        }
    }

    private static TraderEvolutionOrderRequestDto ToTraderEvolutionOrderRequest(OrderRequest request, string tradableInstrumentId) =>
        new()
        {
            TradableInstrumentId = tradableInstrumentId,
            Symbol = request.Symbol,
            Side = request.Side.ToString().ToLowerInvariant(),
            Type = request.Type.ToString().ToLowerInvariant(),
            Qty = request.Quantity,
            Price = request.LimitPrice,
            StopPrice = request.StopPrice,
            Validity = request.TimeInForce.ToString().ToLowerInvariant(),
            ClOrderId = request.CorrelationId
        };

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private async Task<IReadOnlyList<OrderResult>> GetActiveOrdersAsync(string accountId, TradingEnv env, string correlationId, CancellationToken ct)
    {
        var orders = await SendAsync<TraderEvolutionOrdersDataDto>(
            env,
            ApiPath(env, $"accounts/{Uri.EscapeDataString(accountId)}/orders?correlationId={Uri.EscapeDataString(correlationId)}"),
            ct);

        return orders.Orders.Select(row => TraderEvolutionMapper.ToDomainOrderRow(accountId, env, row)).ToList();
    }

    private static bool IsCancellableOrder(OrderResult order) =>
        order.Status is OrderStatus.Pending or OrderStatus.Accepted or OrderStatus.PartiallyFilled;

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
        return client;
    }

    private async Task ApplyRequestAuthAsync(HttpRequestMessage request, TradingEnv env, CancellationToken ct)
    {
        var settings = GetEnvironmentOptions(env);
        if (settings.AuthMode == TraderEvolutionAuthMode.ApiKeyHeaders)
        {
            request.Headers.Remove("X-Api-Key");
            request.Headers.Remove("X-Api-Secret");
            request.Headers.Add("X-Api-Key", settings.ApiKey);
            request.Headers.Add("X-Api-Secret", settings.ApiSecret);
            return;
        }

        var token = await _accessTokenProvider.GetAccessTokenAsync(env, ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task ApplyWebSocketAuthAsync(ClientWebSocket socket, TradingEnv env, TraderEvolutionEnvironmentOptions settings, CancellationToken ct)
    {
        if (settings.AuthMode == TraderEvolutionAuthMode.ApiKeyHeaders)
        {
            socket.Options.SetRequestHeader("X-Api-Key", settings.ApiKey);
            socket.Options.SetRequestHeader("X-Api-Secret", settings.ApiSecret);
            return;
        }

        var token = await _accessTokenProvider.GetAccessTokenAsync(env, ct);
        socket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
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

    private string ApiPath(TradingEnv env, string relativePath)
    {
        var settings = GetEnvironmentOptions(env);
        var basePath = NormalizePath(settings.ApiBasePath);
        return $"{basePath}/{relativePath.TrimStart('/')}";
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return string.Empty;
        }

        return $"/{path.Trim('/')}";
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new BrokerAdapterException("TraderEvolution returned an empty response body.");
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("s", out var statusElement))
        {
            var status = statusElement.GetString();
            if (string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
            {
                var message = document.RootElement.TryGetProperty("errmsg", out var errorElement)
                    ? errorElement.GetString()
                    : "TraderEvolution returned an error response.";

                throw new BrokerAdapterException(BrokerErrorCode.Unknown, message ?? "TraderEvolution returned an error response.");
            }

            if (!document.RootElement.TryGetProperty("d", out var dataElement))
            {
                throw new BrokerAdapterException("TraderEvolution response envelope is missing the 'd' payload.");
            }

            return dataElement.Deserialize<T>(JsonOptions)
                ?? throw new BrokerAdapterException("TraderEvolution response payload could not be deserialized.");
        }

        return document.RootElement.Deserialize<T>(JsonOptions)
            ?? throw new BrokerAdapterException("TraderEvolution response payload could not be deserialized.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw TraderEvolutionBrokerErrorMapper.ToException(response.StatusCode, response.ReasonPhrase, body);
    }

    private static bool IsTransient(Exception exception) =>
        exception is HttpRequestException
        || exception is TimeoutException
        || exception is TaskCanceledException
        || exception is BrokerAdapterException { Code: BrokerErrorCode.BrokerUnavailable or BrokerErrorCode.RateLimited or BrokerErrorCode.Timeout };

    private static Exception Translate(Exception exception) =>
        exception switch
        {
            BrokerAdapterException => exception,
            TaskCanceledException => new BrokerAdapterException(BrokerErrorCode.Timeout, "TraderEvolution transport timed out.", exception),
            HttpRequestException => new BrokerAdapterException(BrokerErrorCode.BrokerUnavailable, "TraderEvolution transport is unavailable.", exception),
            _ => new BrokerAdapterException("TraderEvolution transport failed.", exception)
        };

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

    private static async Task SendTextAsync(ClientWebSocket socket, string message, CancellationToken ct)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, ct);
    }
}
