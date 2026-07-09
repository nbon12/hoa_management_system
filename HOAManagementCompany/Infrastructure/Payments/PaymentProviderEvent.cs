namespace HOAManagementCompany.Infrastructure.Payments;

// <!-- REPOWISE:START domain=payments -->
// Gateway-neutral inbound provider event (015 US5, FR-021): the only shape feature code sees for
// webhooks/reconciliation. Provider-SDK types (Stripe.*) stay confined to Infrastructure/Payments;
// StripeEventTranslator maps raw events to this record. Lives beside the other gateway-neutral
// DTOs (StripePaymentIntentResult, …) so Infrastructure never depends on Features.
// <!-- REPOWISE:END -->

/// <summary>The provider events the platform reacts to. Anything else maps to <see cref="Ignored"/>.</summary>
public enum PaymentProviderEventKind
{
    /// <summary>Unhandled/unknown provider event type — recorded in the inbox, no business effect.</summary>
    Ignored,
    PaymentSucceeded,
    /// <summary>Charge failed OR a settled ACH debit was returned — which of the two is decided
    /// locally from the transaction's state, not from the event (FR-014c).</summary>
    PaymentFailed,
    Refunded,
    DisputeCreated,
    DisputeClosed,
}

/// <summary>
/// Neutral view of an inbound provider event carrying only the fields handlers read. Monetary
/// values are in major units (<see cref="decimal"/>) — minor-unit conversion happens in the
/// gateway layer only.
/// </summary>
public sealed record PaymentProviderEvent(
    string EventId,
    PaymentProviderEventKind Kind,
    string RawType,
    string? PaymentIntentId = null,
    string? ChargeId = null,
    string? LatestChargeId = null,
    decimal? AmountRefunded = null,
    string? FailureCode = null,
    string? FailureMessage = null,
    string? DisputeId = null,
    string? DisputeStatus = null);

/// <summary>
/// Neutral signature-verification failure (raised by the gateway when a webhook payload's
/// signature is invalid), so feature code never catches provider-SDK exception types.
/// </summary>
public sealed class ProviderSignatureVerificationException(string message, Exception? inner = null)
    : Exception(message, inner);
