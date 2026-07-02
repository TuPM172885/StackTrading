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
        publisher.Events.Select(e => e.IdempotencyKey).Should().OnlyHaveUniqueItems();
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

        app.MapPost("/api/accounts", (ProvisionRequest request) =>
        {
            var account = new BrokerAccount(
                $"ACC-{Guid.NewGuid():N}"[..12],
                request.ExternalUserId,
                TradingEnv.Paper,
                AccountStatus.Active,
                request.BaseCurrency,
                request.StartingBalance,
                request.StartingBalance,
                DateTimeOffset.UtcNow,
                request.Metadata);

            _accounts[account.AccountId] = new FakeAccountState(account);
            _streams[account.AccountId] = Channel.CreateUnbounded<BrokerEvent>();
            return Results.Ok(account);
        });

        app.MapPost("/api/accounts/{accountId}/suspend", (string accountId) =>
        {
            if (_accounts.TryGetValue(accountId, out var state))
            {
                state.State = state.State with { Status = AccountStatus.Suspended, UpdatedAt = DateTimeOffset.UtcNow };
            }

            return Results.Ok();
        });

        app.MapDelete("/api/accounts/{accountId}", (string accountId) =>
        {
            _accounts.TryRemove(accountId, out _);
            return Results.Ok();
        });

        app.MapPost("/api/orders", (OrderRequest request) =>
        {
            var order = new OrderResult(
                $"ORD-{Guid.NewGuid():N}"[..12],
                request.AccountId,
                request.Environment,
                OrderStatus.Accepted,
                request.Symbol,
                request.Side,
                request.Quantity,
                request.Quantity,
                1.2345m,
                null,
                DateTimeOffset.UtcNow);

            var account = _accounts[request.AccountId];
            ApplyOrderToPosition(account, request);

            AddEvent(new BrokerEvent(BrokerEventType.OrderAccepted, request.AccountId, request.Environment, request.CorrelationId, $"accept-{order.OrderId}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(order)));
            AddEvent(new BrokerEvent(BrokerEventType.OrderFilled, request.AccountId, request.Environment, request.CorrelationId, $"fill-{order.OrderId}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(order with { Status = OrderStatus.Filled })));
            AddEvent(new BrokerEvent(BrokerEventType.PositionUpdated, request.AccountId, request.Environment, request.CorrelationId, $"pos-{request.AccountId}-{request.Symbol}-{Guid.NewGuid():N}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new { request.AccountId, request.Symbol })));

            return Results.Ok(order);
        });

        app.MapPatch("/api/orders/{orderId}", (string orderId, OrderChange change) =>
        {
            var order = new OrderResult(orderId, change.AccountId, change.Environment, OrderStatus.Accepted, "EURUSD", OrderSide.Buy, change.Quantity ?? 1m, 0m, null, "updated", DateTimeOffset.UtcNow);
            return Results.Ok(order);
        });

        app.MapDelete("/api/orders/{orderId}", (string orderId, string accountId, string correlationId) =>
        {
            AddEvent(new BrokerEvent(BrokerEventType.OrderCancelled, accountId, TradingEnv.Paper, correlationId, $"cancel-{orderId}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new { orderId, accountId })));
            return Results.Ok();
        });

        app.MapGet("/api/accounts/{accountId}/positions", (string accountId) =>
        {
            var positions = _accounts.TryGetValue(accountId, out var state)
                ? state.Positions.Values.ToList()
                : [];

            return Results.Ok(positions);
        });

        app.MapGet("/api/accounts/{accountId}/state", (string accountId) =>
        {
            return _accounts.TryGetValue(accountId, out var state)
                ? Results.Ok(state.State)
                : Results.NotFound();
        });

        app.MapPost("/api/accounts/{accountId}/risk/trim", (string accountId, RiskActionRequest request) =>
        {
            AddEvent(new BrokerEvent(BrokerEventType.ExecutionReport, accountId, request.Environment, request.CorrelationId, $"trim-{accountId}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(request)));
            return Results.Ok();
        });

        app.MapPost("/api/accounts/{accountId}/risk/flatten", (string accountId, RiskActionRequest request) =>
        {
            _accounts[accountId].Positions.Clear();
            AddEvent(new BrokerEvent(BrokerEventType.LiquidationExecuted, accountId, request.Environment, request.CorrelationId, $"flatten-{accountId}", DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(request)));
            return Results.Ok();
        });

        app.Map("/ws/accounts/{accountId}", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var accountId = context.Request.RouteValues["accountId"]?.ToString() ?? string.Empty;
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            foreach (var brokerEvent in _events.Where(item => item.AccountId == accountId))
            {
                var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(brokerEvent));
                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, context.RequestAborted);
            }

            if (_streams.TryGetValue(accountId, out var stream))
            {
                while (!context.RequestAborted.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, timeout.Token);

                    try
                    {
                        var brokerEvent = await stream.Reader.ReadAsync(linked.Token);
                        var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(brokerEvent));
                        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, context.RequestAborted);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", context.RequestAborted);
        });

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

    private static void ApplyOrderToPosition(FakeAccountState account, OrderRequest request)
    {
        account.Positions.TryGetValue(request.Symbol, out var existingPosition);
        var existingSignedQuantity = existingPosition is null
            ? 0m
            : existingPosition.Side == PositionSide.Long ? existingPosition.Quantity : -existingPosition.Quantity;

        var orderSignedQuantity = request.Side == OrderSide.Buy ? request.Quantity : -request.Quantity;
        var newSignedQuantity = existingSignedQuantity + orderSignedQuantity;
        if (newSignedQuantity == 0)
        {
            account.Positions.Remove(request.Symbol);
            return;
        }

        var side = newSignedQuantity > 0 ? PositionSide.Long : PositionSide.Short;
        account.Positions[request.Symbol] = new Position(
            request.AccountId,
            request.Symbol,
            side,
            Math.Abs(newSignedQuantity),
            request.LimitPrice ?? request.StopPrice ?? 1.2345m,
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
