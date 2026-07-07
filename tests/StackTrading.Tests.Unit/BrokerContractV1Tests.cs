using System.Text.Json;
using FluentAssertions;
using StackTrading.Contracts;

namespace StackTrading.Tests.Unit;

public sealed class BrokerContractV1Tests
{
    [Fact]
    public void IBrokerAdapter_ShouldExposeStableV1Methods()
    {
        var methodNames = typeof(IBrokerAdapter)
            .GetMethods()
            .Select(method => method.Name)
            .OrderBy(name => name)
            .ToArray();

        methodNames.Should().Equal(
            "CancelOrderAsync",
            "CloseAccountAsync",
            "CreateAccountAsync",
            "FlattenAllAsync",
            "GetAccountStateAsync",
            "GetPositionsAsync",
            "ModifyOrderAsync",
            "PlaceOrderAsync",
            "SubscribeAsync",
            "SuspendAccountAsync",
            "TrimToComplianceAsync");
    }

    [Fact]
    public void BrokerEventType_ShouldKeepStableV1NumericValues()
    {
        ((int)BrokerEventType.OrderAccepted).Should().Be(0);
        ((int)BrokerEventType.OrderFilled).Should().Be(1);
        ((int)BrokerEventType.OrderCancelled).Should().Be(2);
        ((int)BrokerEventType.OrderRejected).Should().Be(3);
        ((int)BrokerEventType.PositionUpdated).Should().Be(4);
        ((int)BrokerEventType.AccountStateChanged).Should().Be(5);
        ((int)BrokerEventType.ExecutionReport).Should().Be(6);
        ((int)BrokerEventType.MarginBreach).Should().Be(7);
        ((int)BrokerEventType.DrawdownBreach).Should().Be(8);
        ((int)BrokerEventType.LiquidationExecuted).Should().Be(9);
    }

    [Fact]
    public void BrokerErrorCode_ShouldKeepStableV1NumericValues()
    {
        ((int)BrokerErrorCode.Unknown).Should().Be(0);
        ((int)BrokerErrorCode.AuthenticationFailed).Should().Be(1);
        ((int)BrokerErrorCode.AuthorizationFailed).Should().Be(2);
        ((int)BrokerErrorCode.ValidationFailed).Should().Be(3);
        ((int)BrokerErrorCode.NotFound).Should().Be(4);
        ((int)BrokerErrorCode.RateLimited).Should().Be(5);
        ((int)BrokerErrorCode.Timeout).Should().Be(6);
        ((int)BrokerErrorCode.BrokerUnavailable).Should().Be(7);
        ((int)BrokerErrorCode.EnvironmentMismatch).Should().Be(8);
        ((int)BrokerErrorCode.DuplicateRequest).Should().Be(9);
        ((int)BrokerErrorCode.NotSupported).Should().Be(10);
    }

    [Fact]
    public void BrokerEvent_ShouldSerializeV1Envelope()
    {
        var payload = JsonSerializer.SerializeToElement(new { orderId = "ORD-1" });
        var brokerEvent = new BrokerEvent(
            BrokerEventType.OrderFilled,
            "ACC-1",
            TradingEnv.Paper,
            "corr-1",
            "idem-1",
            DateTimeOffset.Parse("2026-07-07T00:00:00Z"),
            payload);

        var json = JsonSerializer.SerializeToElement(brokerEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.GetProperty("eventType").GetInt32().Should().Be((int)BrokerEventType.OrderFilled);
        json.GetProperty("accountId").GetString().Should().Be("ACC-1");
        json.GetProperty("environment").GetInt32().Should().Be((int)TradingEnv.Paper);
        json.GetProperty("correlationId").GetString().Should().Be("corr-1");
        json.GetProperty("idempotencyKey").GetString().Should().Be("idem-1");
        json.GetProperty("payload").GetProperty("orderId").GetString().Should().Be("ORD-1");
    }
}
