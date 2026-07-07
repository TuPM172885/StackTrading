using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackTrading.Application;
using StackTrading.Contracts;
using StackTrading.Host.Service.Controllers;
using StackTrading.Infrastructure.TraderEvolution;

namespace StackTrading.Tests.Integration;

public sealed class TraderEvolutionIntegrationTests : IAsyncLifetime
{
    private readonly FakeTraderEvolutionBroker _fakeBroker = new();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _fakeBroker.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Kafka:Enabled"] = "false",
                        ["TraderEvolution:Paper:ApiBaseUrl"] = _fakeBroker.HttpBaseUrl,
                        ["TraderEvolution:Paper:WebSocketBaseUrl"] = _fakeBroker.WebSocketBaseUrl,
                        ["TraderEvolution:Paper:ApiKey"] = "paper-key",
                        ["TraderEvolution:Paper:ApiSecret"] = "paper-secret",
                        ["TraderEvolution:Paper:Enabled"] = "true",
                        ["TraderEvolution:Live:ApiBaseUrl"] = "http://127.0.0.1:6501",
                        ["TraderEvolution:Live:WebSocketBaseUrl"] = "ws://127.0.0.1:6501",
                        ["TraderEvolution:Live:ApiKey"] = "live-key",
                        ["TraderEvolution:Live:ApiSecret"] = "live-secret",
                        ["TraderEvolution:Live:Enabled"] = "false"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    var descriptor = services.Single(service => service.ServiceType == typeof(IBrokerEventPublisher));
                    services.Remove(descriptor);
                    services.AddSingleton<RecordingPublisher>();
                    services.AddSingleton<IBrokerEventPublisher>(sp => sp.GetRequiredService<RecordingPublisher>());
                });
            });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task EndToEnd_ShouldPlaceOrderAndPublishNormalizedEvents()
    {
        var client = _client!;
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/accounts",
            new CreateAccountCommand(TradingEnv.Paper, "user-1", "challenge-1", "USD", 100_000m, null, null));
        createResponse.EnsureSuccessStatusCode();

        var account = await createResponse.Content.ReadFromJsonAsync<BrokerAccount>();
        account.Should().NotBeNull();

        var orderResponse = await client.PostAsJsonAsync(
            "/api/v1/orders",
            new PlaceOrderCommand(account!.AccountId, TradingEnv.Paper, "EURUSD", OrderSide.Buy, OrderType.Market, 2m, null, null, TimeInForce.Day, "corr-order-1", null, null));
        orderResponse.EnsureSuccessStatusCode();

        await client.PostAsync($"/api/v1/accounts/{account.AccountId}/subscriptions?env=Paper", null);
        await Task.Delay(1200);

        var positions = await client.GetFromJsonAsync<List<Position>>($"/api/v1/accounts/{account.AccountId}/positions?env=Paper&correlationId=read-1");
        positions.Should().NotBeNull();
        positions.Should().ContainSingle();
        positions![0].Quantity.Should().Be(2m);

        var flattenResponse = await client.PostAsJsonAsync(
            $"/api/v1/accounts/{account.AccountId}/risk/flatten",
            new RiskCommand(TradingEnv.Paper, "integration cleanup", "integration-test", null, "corr-flatten-1", null));
        flattenResponse.EnsureSuccessStatusCode();

        var flattenedPositions = await client.GetFromJsonAsync<List<Position>>($"/api/v1/accounts/{account.AccountId}/positions?env=Paper&correlationId=read-2");
        flattenedPositions.Should().NotBeNull();
        flattenedPositions.Should().BeEmpty();

        var publisher = _factory!.Services.GetRequiredService<RecordingPublisher>();
        publisher.Events.Should().Contain(e => e.EventType == BrokerEventType.OrderFilled && e.AccountId == account.AccountId);
        publisher.Events.Should().Contain(e => e.EventType == BrokerEventType.AccountStateChanged && e.AccountId == account.AccountId);
        publisher.Events.Select(e => e.IdempotencyKey).Should().OnlyHaveUniqueItems();

        var closeResponse = await client.DeleteAsync($"/api/v1/accounts/{account.AccountId}?env=Paper&correlationId=corr-close-1");
        closeResponse.EnsureSuccessStatusCode();

        var closedState = await client.GetFromJsonAsync<AccountState>($"/api/v1/accounts/{account.AccountId}/state?env=Paper&correlationId=read-closed");
        closedState!.Status.Should().Be(AccountStatus.Closed);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _fakeBroker.DisposeAsync();
    }

    public sealed class RecordingPublisher : IBrokerEventPublisher
    {
        public ConcurrentBag<BrokerEvent> Events { get; } = [];

        public Task PublishAsync(BrokerEvent brokerEvent, CancellationToken ct)
        {
            Events.Add(brokerEvent);
            return Task.CompletedTask;
        }
    }
}

internal sealed class FakeTraderEvolutionBroker : IAsyncDisposable
{
    private readonly List<BrokerEvent> _events = [];
    private readonly ConcurrentDictionary<string, FakeAccountState> _accounts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Channel<BrokerEvent>> _streams = new(StringComparer.Ordinal);
    private WebApplication? _app;

    public string HttpBaseUrl { get; private set; } = string.Empty;
    public string WebSocketBaseUrl { get; private set; } = string.Empty;

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.UseWebSockets();

        var seededAccount = new BrokerAccount(
            "3243753",
            "user-1",
            TradingEnv.Paper,
            AccountStatus.Active,
            "USD",
            100_000m,
            100_000m,
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { ["source"] = "fake-traderevolution" });

        _accounts[seededAccount.AccountId] = new FakeAccountState(seededAccount);
        _streams[seededAccount.AccountId] = Channel.CreateUnbounded<BrokerEvent>();

        app.MapGet("/traderevolution/v1/accounts", () =>
        {
            var accounts = _accounts.Values.Select(state => new
            {
                id = state.State.AccountId,
                name = "test",
                type = "demo",
                currency = "USD",
                status = "ACTIVE",
                tradingRules = new
                {
                    supportBrackets = true,
                    supportTrailingStop = true
                },
                riskRules = new
                {
                    maxTrailingDrawdown = 800,
                    maxPendingOrdersNumber = 60
                },
                marginRules = new
                {
                    stopOutLevel = 100,
                    marginWarningLevel = 90
                }
            }).ToArray();

            return OkData(new { accounts });
        });

        app.MapGet("/traderevolution/v1/accounts/{accountId}/instruments", (string accountId) =>
        {
            var instruments = new[]
            {
                new
                {
                    id = "1001",
                    tradableInstrumentId = "1001",
                    name = "EURUSD",
                    symbol = "EURUSD",
                    ticker = "EURUSD",
                    type = "forex",
                    exchange = "FX"
                },
                new
                {
                    id = "2001",
                    tradableInstrumentId = "2001",
                    name = "NQ",
                    symbol = "NQ",
                    ticker = "NQ",
                    type = "futures",
                    exchange = "CME"
                }
            };

            return OkData(new { instruments });
        });

        app.MapPost("/traderevolution/v1/accounts/{accountId}/closeAccount", (string accountId) =>
        {
            if (_accounts.TryGetValue(accountId, out var state))
            {
                state.State = state.State with { Status = AccountStatus.Closed, UpdatedAt = DateTimeOffset.UtcNow };
            }

            return OkData(new { requestId = $"close-{accountId}" });
        });

        app.MapPost("/traderevolution/v1/accounts/{accountId}/orders", (string accountId, TraderEvolutionOrderRequestDto request) =>
        {
            var side = request.Side.Equals("sell", StringComparison.OrdinalIgnoreCase) ? OrderSide.Sell : OrderSide.Buy;
            var order = new OrderResult(
                $"ORD-{Guid.NewGuid():N}"[..12],
                accountId,
                TradingEnv.Paper,
                OrderStatus.Accepted,
                request.Symbol,
                side,
                request.Qty,
                request.Qty,
                1.2345m,
                null,
                DateTimeOffset.UtcNow);

            var account = _accounts[accountId];
            ApplyOrderToPosition(account, accountId, request);

            AddEvent(new BrokerEvent(BrokerEventType.OrderAccepted, accountId, TradingEnv.Paper, request.ClOrderId, $"accept-{order.OrderId}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(order)));
            AddEvent(new BrokerEvent(BrokerEventType.OrderFilled, accountId, TradingEnv.Paper, request.ClOrderId, $"fill-{order.OrderId}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(order with { Status = OrderStatus.Filled })));
            AddEvent(new BrokerEvent(BrokerEventType.PositionUpdated, accountId, TradingEnv.Paper, request.ClOrderId, $"pos-{accountId}-{request.Symbol}-{Guid.NewGuid():N}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new { accountId, request.Symbol })));
            AddEvent(new BrokerEvent(BrokerEventType.AccountStateChanged, accountId, TradingEnv.Paper, request.ClOrderId, $"acct-{accountId}-{Guid.NewGuid():N}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(account.State)));

            return OkData(order);
        });

        app.MapPatch("/traderevolution/v1/accounts/{accountId}/orders/{orderId}", (string accountId, string orderId, OrderChange change) =>
        {
            var order = new OrderResult(orderId, accountId, change.Environment, OrderStatus.Accepted, "EURUSD", OrderSide.Buy, change.Quantity ?? 1m, 0m, null, "updated", DateTimeOffset.UtcNow);
            return OkData(order);
        });

        app.MapGet("/traderevolution/v1/accounts/{accountId}/orders", (string accountId) =>
        {
            return OkData(new { orders = Array.Empty<object[]>() });
        });

        app.MapDelete("/traderevolution/v1/accounts/{accountId}/orders/{orderId}", (string accountId, string orderId, string correlationId) =>
        {
            AddEvent(new BrokerEvent(BrokerEventType.OrderCancelled, accountId, TradingEnv.Paper, correlationId, $"cancel-{orderId}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new { orderId, accountId })));
            return Results.Ok();
        });

        app.MapGet("/traderevolution/v1/accounts/{accountId}/positions", (string accountId) =>
        {
            var positions = _accounts.TryGetValue(accountId, out var state)
                ? state.Positions.Values.ToList()
                : [];

            return OkData(new { positions });
        });

        app.MapGet("/traderevolution/v1/accounts/{accountId}/state", (string accountId) =>
        {
            return _accounts.TryGetValue(accountId, out var state)
                ? OkData(state.State)
                : Results.NotFound();
        });

        app.MapPost("/traderevolution/v1/accounts/{accountId}/risk/trim", (string accountId, RiskActionRequest request) =>
        {
            AddEvent(new BrokerEvent(BrokerEventType.ExecutionReport, accountId, request.Environment, request.CorrelationId, $"trim-{accountId}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(request)));
            return Results.Ok();
        });

        app.MapPost("/traderevolution/v1/accounts/{accountId}/risk/flatten", (string accountId, RiskActionRequest request) =>
        {
            _accounts[accountId].Positions.Clear();
            AddEvent(new BrokerEvent(BrokerEventType.LiquidationExecuted, accountId, request.Environment, request.CorrelationId, $"flatten-{accountId}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(request)));
            return Results.Ok();
        });

        app.Map("/traderevolution/v1/stream/tradeEvents", (RequestDelegate)HandleTradeEventsStreamAsync);
        app.Map("/traderevolution/v1/stream/accounts", (RequestDelegate)HandleAccountsStreamAsync);

        await app.StartAsync();
        _app = app;
        var address = app.Urls.Single();
        HttpBaseUrl = address;
        WebSocketBaseUrl = address.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);
    }

    private void AddEvent(BrokerEvent brokerEvent)
    {
        if (_events.All(existing => existing.IdempotencyKey != brokerEvent.IdempotencyKey))
        {
            _events.Add(brokerEvent);
            if (_streams.TryGetValue(brokerEvent.AccountId, out var stream))
            {
                stream.Writer.TryWrite(brokerEvent);
            }
        }
    }

    private async Task HandleTradeEventsStreamAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        _ = Task.Run(() => DrainAndAckAsync(socket, context.RequestAborted), context.RequestAborted);

        await SendTextAsync(socket, """{"event":"PING","t":1}""", context.RequestAborted);

        foreach (var brokerEvent in _events)
        {
            await SendBrokerEventAsync(socket, brokerEvent, context.RequestAborted);
        }

        while (!context.RequestAborted.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, timeout.Token);

            try
            {
                var readTasks = _streams.Values.Select(stream => stream.Reader.ReadAsync(linked.Token).AsTask()).ToArray();
                var completed = await Task.WhenAny(readTasks);
                await SendBrokerEventAsync(socket, await completed, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", context.RequestAborted);
    }

    private static async Task HandleAccountsStreamAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await DrainAndAckAsync(socket, context.RequestAborted);
    }

    private static async Task DrainAndAckAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            try
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                if (root.TryGetProperty("event", out var eventProp)
                    && string.Equals(eventProp.GetString(), "subscribe", StringComparison.OrdinalIgnoreCase))
                {
                    var requestId = root.TryGetProperty("requestId", out var rid) ? rid.GetInt32() : 0;
                    var ack = JsonSerializer.Serialize(new { s = "ok", @event = "subscribe", requestId });
                    var ackBytes = Encoding.UTF8.GetBytes(ack);
                    await socket.SendAsync(ackBytes, WebSocketMessageType.Text, true, ct);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (WebSocketException)
            {
                return;
            }
        }
    }

    private static async Task SendBrokerEventAsync(WebSocket socket, BrokerEvent brokerEvent, CancellationToken ct)
    {
        string st;
        string json;

        switch (brokerEvent.EventType)
        {
            case BrokerEventType.OrderAccepted:
            case BrokerEventType.OrderFilled:
            case BrokerEventType.OrderCancelled:
            case BrokerEventType.OrderRejected:
                st = "orders";
                var orderStatus = brokerEvent.EventType switch
                {
                    BrokerEventType.OrderFilled => "filled",
                    BrokerEventType.OrderCancelled => "cancelled",
                    BrokerEventType.OrderRejected => "rejected",
                    _ => "accepted"
                };
                var orderId = brokerEvent.Payload.TryGetProperty("OrderId", out var oid) ? oid.GetString() ?? "unknown" : "unknown";
                var symbol = brokerEvent.Payload.TryGetProperty("Symbol", out var sym) ? sym.GetString() ?? "UNKNOWN" : "UNKNOWN";
                var qty = brokerEvent.Payload.TryGetProperty("Quantity", out var q) ? q.GetDecimal() : 0m;
                var sideVal = brokerEvent.Payload.TryGetProperty("Side", out var sv) && sv.GetInt32() == 1 ? "sell" : "buy";
                var acctId = brokerEvent.AccountId;
                json = $$$"""{"st":"{{{st}}}","accountId":"{{{acctId}}}","d":{"orders":[{"orderId":"{{{orderId}}}","accountId":"{{{acctId}}}","symbol":"{{{symbol}}}","side":"{{{sideVal}}}","status":"{{{orderStatus}}}","qty":{{{qty}}}}]}}""";
                break;
            case BrokerEventType.PositionUpdated:
                st = "openPositions";
                json = $$$"""{"st":"{{{st}}}","accountId":"{{{brokerEvent.AccountId}}}","d":{{{JsonSerializer.Serialize(brokerEvent.Payload)}}}}""";
                break;
            case BrokerEventType.ExecutionReport:
                st = "executions";
                json = $$$"""{"st":"{{{st}}}","accountId":"{{{brokerEvent.AccountId}}}","d":{{{JsonSerializer.Serialize(brokerEvent.Payload)}}}}""";
                break;
            case BrokerEventType.MarginBreach:
                st = "marginWarning";
                json = $$$"""{"st":"{{{st}}}","accountId":"{{{brokerEvent.AccountId}}}","d":{{{JsonSerializer.Serialize(brokerEvent.Payload)}}}}""";
                break;
            case BrokerEventType.DrawdownBreach:
                st = "riskRules";
                json = $$$"""{"st":"{{{st}}}","accountId":"{{{brokerEvent.AccountId}}}","d":{{{JsonSerializer.Serialize(brokerEvent.Payload)}}}}""";
                break;
            case BrokerEventType.LiquidationExecuted:
                st = "stopOut";
                json = $$$"""{"st":"{{{st}}}","accountId":"{{{brokerEvent.AccountId}}}","d":{{{JsonSerializer.Serialize(brokerEvent.Payload)}}}}""";
                break;
            default:
                st = "accountDetailsData";
                json = $$$"""{"st":"{{{st}}}","accountId":"{{{brokerEvent.AccountId}}}","d":{{{JsonSerializer.Serialize(brokerEvent.Payload)}}}}""";
                break;
        }

        var buffer = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, ct);
    }

    private static async Task SendTextAsync(WebSocket socket, string message, CancellationToken ct)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, ct);
    }

    private static void ApplyOrderToPosition(FakeAccountState account, string accountId, TraderEvolutionOrderRequestDto request)
    {
        account.Positions.TryGetValue(request.Symbol, out var existingPosition);
        var existingSignedQuantity = existingPosition is null
            ? 0m
            : existingPosition.Side == PositionSide.Long ? existingPosition.Quantity : -existingPosition.Quantity;

        var orderSignedQuantity = request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase) ? request.Qty : -request.Qty;
        var newSignedQuantity = existingSignedQuantity + orderSignedQuantity;
        if (newSignedQuantity == 0)
        {
            account.Positions.Remove(request.Symbol);
            return;
        }

        var side = newSignedQuantity > 0 ? PositionSide.Long : PositionSide.Short;
        account.Positions[request.Symbol] = new Position(
            accountId,
            request.Symbol,
            side,
            Math.Abs(newSignedQuantity),
            request.Price ?? request.StopPrice ?? 1.2345m,
            15m,
            0m,
            DateTimeOffset.UtcNow);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private static IResult OkData<T>(T data) => Results.Ok(new { s = "ok", d = data });

    private sealed class FakeAccountState
    {
        public FakeAccountState(BrokerAccount account)
        {
            State = new AccountState(account.AccountId, account.Environment, account.Status, account.Balance, account.Equity, 0m, account.Equity, 0m, 0m, DateTimeOffset.UtcNow);
        }

        public AccountState State { get; set; }
        public Dictionary<string, Position> Positions { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
