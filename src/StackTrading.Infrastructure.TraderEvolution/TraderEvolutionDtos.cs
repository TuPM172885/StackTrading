using System.Text.Json;
using StackTrading.Contracts;

namespace StackTrading.Infrastructure.TraderEvolution;

public sealed class TraderEvolutionAccountDto
{
    public string AccountId { get; init; } = string.Empty;
    public string ExternalUserId { get; init; } = string.Empty;
    public JsonElement Environment { get; init; }
    public JsonElement Status { get; init; }
    public string BaseCurrency { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public decimal Equity { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed class TraderEvolutionOrderDto
{
    public string OrderId { get; init; } = string.Empty;
    public string AccountId { get; init; } = string.Empty;
    public JsonElement Environment { get; init; }
    public JsonElement Status { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public JsonElement Side { get; init; }
    public decimal Quantity { get; init; }
    public decimal FilledQuantity { get; init; }
    public decimal? AverageFillPrice { get; init; }
    public string? BrokerMessage { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public sealed class TraderEvolutionPositionDto
{
    public string AccountId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public JsonElement Side { get; init; }
    public decimal Quantity { get; init; }
    public decimal AveragePrice { get; init; }
    public decimal UnrealizedPnl { get; init; }
    public decimal? EstimatedSwap { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class TraderEvolutionAccountStateDto
{
    public string AccountId { get; init; } = string.Empty;
    public JsonElement Environment { get; init; }
    public JsonElement Status { get; init; }
    public decimal Balance { get; init; }
    public decimal Equity { get; init; }
    public decimal MarginUsed { get; init; }
    public decimal MarginAvailable { get; init; }
    public decimal DailyLoss { get; init; }
    public decimal TrailingDrawdown { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class TraderEvolutionBrokerEventDto
{
    public JsonElement EventType { get; init; }
    public string AccountId { get; init; } = string.Empty;
    public JsonElement Environment { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
    public JsonElement Payload { get; init; }
}

public sealed class TraderEvolutionErrorDto
{
    public string? Code { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    public string? CorrelationId { get; init; }
}
