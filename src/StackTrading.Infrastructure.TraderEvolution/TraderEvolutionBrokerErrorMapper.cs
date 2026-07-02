using System.Net;
using System.Text.Json;
using StackTrading.Contracts;

namespace StackTrading.Infrastructure.TraderEvolution;

public static class TraderEvolutionBrokerErrorMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static BrokerAdapterException ToException(HttpStatusCode statusCode, string? reasonPhrase, string body)
    {
        var error = TryReadError(body);
        var message = error?.Message ?? error?.Error ?? body;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = reasonPhrase ?? "TraderEvolution call failed.";
        }

        return new BrokerAdapterException(MapStatusCode(statusCode, error?.Code), $"TraderEvolution call failed with {(int)statusCode}: {message}");
    }

    public static BrokerErrorCode MapStatusCode(HttpStatusCode statusCode, string? brokerCode)
    {
        var normalizedBrokerCode = string.Concat((brokerCode ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();
        if (normalizedBrokerCode is "duplicaterequest" or "duplicate" or "idempotencyconflict")
        {
            return BrokerErrorCode.DuplicateRequest;
        }

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => BrokerErrorCode.AuthenticationFailed,
            HttpStatusCode.Forbidden => BrokerErrorCode.AuthorizationFailed,
            HttpStatusCode.NotFound => BrokerErrorCode.NotFound,
            HttpStatusCode.Conflict => BrokerErrorCode.DuplicateRequest,
            HttpStatusCode.TooManyRequests => BrokerErrorCode.RateLimited,
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => BrokerErrorCode.ValidationFailed,
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => BrokerErrorCode.Timeout,
            >= HttpStatusCode.InternalServerError => BrokerErrorCode.BrokerUnavailable,
            _ => BrokerErrorCode.Unknown
        };
    }

    private static TraderEvolutionErrorDto? TryReadError(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<TraderEvolutionErrorDto>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
