using System.Text.Json;
using HOAManagementCompany.Infrastructure.Payments;
using Stripe;
using Xunit;

namespace HOAManagementCompany.UnitTests.Payments;

/// <summary>
/// 015 US5 (FR-021, contracts/provider-event-model.md): the exhaustive Stripe→neutral kind
/// mapping, exercised against <see cref="StripeEventTranslator"/> — the coverable pure translator.
/// Constructing Stripe SDK POCOs here is fine: the architecture rule confines the namespace
/// within the app assembly; tests live outside it.
/// </summary>
public class ProviderEventMappingTests
{
    private static Event Evt(string type, object dataObject) => EventUtility.ParseEvent(
        JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            @object = "event",
            type,
            api_version = StripeConfiguration.ApiVersion,
            request = (string?)null,
            data = new { @object = dataObject },
        }));

    [Theory]
    [InlineData("payment_intent.succeeded", PaymentProviderEventKind.PaymentSucceeded)]
    [InlineData("payment_intent.payment_failed", PaymentProviderEventKind.PaymentFailed)]
    public void IntentEvents_MapKindAndFields(string type, PaymentProviderEventKind expected)
    {
        var evt = Evt(type, new
        {
            id = "pi_123",
            @object = "payment_intent",
            latest_charge = "ch_123",
            last_payment_error = new { code = "card_declined", message = "declined" },
        });

        var result = StripeEventTranslator.Translate(evt);

        Assert.Equal(expected, result.Kind);
        Assert.Equal(type, result.RawType);
        Assert.Equal("pi_123", result.PaymentIntentId);
        Assert.Equal("ch_123", result.LatestChargeId);
        Assert.Equal("card_declined", result.FailureCode);
        Assert.Equal("declined", result.FailureMessage);
    }

    [Theory]
    [InlineData("charge.refunded")]
    [InlineData("charge.refund.updated")]
    public void ChargeRefundEvents_MapToRefunded_WithMajorUnits(string type)
    {
        var evt = Evt(type, new { id = "ch_777", @object = "charge", amount_refunded = 12345 });

        var result = StripeEventTranslator.Translate(evt);

        Assert.Equal(PaymentProviderEventKind.Refunded, result.Kind);
        Assert.Equal("ch_777", result.ChargeId);
        Assert.Equal(123.45m, result.AmountRefunded);   // minor-unit conversion happens HERE only
    }

    [Theory]
    [InlineData("charge.dispute.created", PaymentProviderEventKind.DisputeCreated, "warning_needs_response")]
    [InlineData("charge.dispute.closed", PaymentProviderEventKind.DisputeClosed, "won")]
    [InlineData("charge.dispute.closed", PaymentProviderEventKind.DisputeClosed, "lost")]
    public void DisputeEvents_MapKindChargeAndStatus(string type, PaymentProviderEventKind expected, string status)
    {
        var evt = Evt(type, new { id = "dp_9", @object = "dispute", charge = "ch_9", status });

        var result = StripeEventTranslator.Translate(evt);

        Assert.Equal(expected, result.Kind);
        Assert.Equal("ch_9", result.ChargeId);
        Assert.Equal("dp_9", result.DisputeId);
        Assert.Equal(status, result.DisputeStatus);
    }

    [Theory]
    [InlineData("customer.created")]
    [InlineData("invoice.paid")]
    [InlineData("payment_method.attached")]
    public void UnhandledEventTypes_MapToIgnored(string type)
    {
        var evt = Evt(type, new { id = "obj_1", @object = "customer" });

        var result = StripeEventTranslator.Translate(evt);

        Assert.Equal(PaymentProviderEventKind.Ignored, result.Kind);
        Assert.Equal(type, result.RawType);
    }
}
