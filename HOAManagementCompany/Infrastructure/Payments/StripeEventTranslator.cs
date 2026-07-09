using HOAManagementCompany.Domain.Payments;
using Stripe;

namespace HOAManagementCompany.Infrastructure.Payments;

/// <summary>
/// Pure Stripe→neutral event mapping (015 US5, FR-021). Deliberately NOT
/// <c>[ExcludeFromCodeCoverage]</c>: this is contract-mapping logic, unit-tested via the kind
/// table (ProviderEventMappingTests) and counted by the coverage gate — only the gateway's
/// SDK-I/O surface is excluded. Exhaustive over the event types the platform handles; everything
/// else maps to <see cref="PaymentProviderEventKind.Ignored"/> (inbox-recorded, no effect).
/// </summary>
public static class StripeEventTranslator
{
    public static PaymentProviderEvent Translate(Event evt) => evt.Type switch
    {
        "payment_intent.succeeded" => FromIntent(evt, PaymentProviderEventKind.PaymentSucceeded),
        "payment_intent.payment_failed" => FromIntent(evt, PaymentProviderEventKind.PaymentFailed),
        "charge.refunded" or "charge.refund.updated" => FromCharge(evt),
        "charge.dispute.created" => FromDispute(evt, PaymentProviderEventKind.DisputeCreated),
        "charge.dispute.closed" => FromDispute(evt, PaymentProviderEventKind.DisputeClosed),
        _ => new PaymentProviderEvent(evt.Id, PaymentProviderEventKind.Ignored, evt.Type),
    };

    private static PaymentProviderEvent FromIntent(Event evt, PaymentProviderEventKind kind)
    {
        var pi = (PaymentIntent)evt.Data.Object;
        return new PaymentProviderEvent(
            evt.Id, kind, evt.Type,
            PaymentIntentId: pi.Id,
            LatestChargeId: pi.LatestChargeId,
            FailureCode: pi.LastPaymentError?.Code,
            FailureMessage: pi.LastPaymentError?.Message);
    }

    private static PaymentProviderEvent FromCharge(Event evt)
    {
        var charge = (Charge)evt.Data.Object;
        return new PaymentProviderEvent(
            evt.Id, PaymentProviderEventKind.Refunded, evt.Type,
            ChargeId: charge.Id,
            AmountRefunded: MoneyPolicy.FromCents(charge.AmountRefunded));
    }

    private static PaymentProviderEvent FromDispute(Event evt, PaymentProviderEventKind kind)
    {
        var dispute = (Dispute)evt.Data.Object;
        return new PaymentProviderEvent(
            evt.Id, kind, evt.Type,
            ChargeId: dispute.ChargeId,
            DisputeId: dispute.Id,
            DisputeStatus: dispute.Status);
    }
}
