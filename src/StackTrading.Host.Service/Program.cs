using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackTrading.Application;
using StackTrading.Contracts;
using StackTrading.Host.Service.HostedServices;
using StackTrading.Host.Service.Infrastructure;
using StackTrading.Infrastructure.TraderEvolution;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddSingleton<IEventDeduplicator, InMemoryEventDeduplicator>();
builder.Services.AddSingleton<ISubscriptionRegistry, InMemorySubscriptionRegistry>();
builder.Services.AddSingleton<IBrokerEventPublisher, KafkaBrokerEventPublisher>();
builder.Services.AddSingleton<IBrokerAdapter, BrokerAdapterOrchestrator>();
builder.Services.AddTraderEvolutionInfrastructure(builder.Configuration);
builder.Services.AddHostedService<BrokerStreamBackgroundService>();
builder.Services.AddHealthChecks().AddCheck<BrokerRuntimeHealthCheck>("broker_runtime");

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("StackTrading.Host.Service"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

var subscriptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TraderEvolutionOptions>>().Value.PreconfiguredSubscriptions;
var registry = app.Services.GetRequiredService<ISubscriptionRegistry>();
foreach (var subscription in subscriptions)
{
    registry.TryRegister(new BrokerSubscription(subscription.AccountId, subscription.Environment));
}

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledException");
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        logger.LogError(exception, "Unhandled exception");
        context.Response.StatusCode = exception is BrokerAdapterException ? StatusCodes.Status502BadGateway : StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = exception?.Message ?? "Unexpected error" });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program;
