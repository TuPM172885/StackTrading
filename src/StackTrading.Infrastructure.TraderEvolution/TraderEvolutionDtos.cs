using System.Text.Json;
using StackTrading.Contracts;

namespace StackTrading.Infrastructure.TraderEvolution;

public sealed class TraderEvolutionResponse<T>
{
    public string? S { get; init; }
    public T? D { get; init; }
    public string? Errmsg { get; init; }
}

public sealed class TraderEvolutionAccountsDataDto
{
    public IReadOnlyList<TraderEvolutionAccountSettingsDto> Accounts { get; init; } = [];
}

public sealed class TraderEvolutionAccountSettingsDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public JsonElement TradingRules { get; init; }
    public JsonElement RiskRules { get; init; }
    public JsonElement MarginRules { get; init; }
}

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

public sealed class TraderEvolutionPositionsDataDto
{
    public IReadOnlyList<JsonElement> Positions { get; init; } = [];
}

public sealed class TraderEvolutionInstrumentsDataDto
{
    public IReadOnlyList<TraderEvolutionInstrumentDto> Instruments { get; init; } = [];
}

public sealed class TraderEvolutionInstrumentDto
{
    public string Id { get; init; } = string.Empty;
    public string TradableInstrumentId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Ticker { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Exchange { get; init; } = string.Empty;
}

public sealed class TraderEvolutionOrderRequestDto
{
    public required string TradableInstrumentId { get; init; }
    public required string Symbol { get; init; }
    public required string Side { get; init; }
    public required string Type { get; init; }
    public required decimal Qty { get; init; }
    public decimal? Price { get; init; }
    public decimal? StopPrice { get; init; }
    public required string Validity { get; init; }
    public required string ClOrderId { get; init; }
}

public sealed class TraderEvolutionOrderChangeDto
{
    public decimal? Qty { get; init; }
    public decimal? Price { get; init; }
    public decimal? StopPrice { get; init; }
    public string? Validity { get; init; }
    public required string ClOrderId { get; init; }
}

public sealed class TraderEvolutionOrdersDataDto
{
    public IReadOnlyList<JsonElement> Orders { get; init; } = [];
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

public sealed class TraderEvolutionAccountStateDataDto
{
    public JsonElement AccountDetails { get; init; }
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

public sealed class TraderEvolutionStreamSubscriptionDto
{
    public required string Event { get; init; }
    public required int RequestId { get; init; }
    public required TraderEvolutionStreamSubscriptionPayloadDto Payload { get; init; }
}

public sealed class TraderEvolutionStreamSubscriptionPayloadDto
{
    public required string AccountId { get; init; }
    public required string St { get; init; }
}

public sealed class TraderEvolutionErrorDto
{
    public string? Code { get; init; }
    public string? S { get; init; }
    public string? Errmsg { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    public string? CorrelationId { get; init; }
}
