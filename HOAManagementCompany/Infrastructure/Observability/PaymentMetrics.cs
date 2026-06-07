using System.Diagnostics.Metrics;

namespace HOAManagementCompany.Infrastructure.Observability;

/// <summary>
/// Custom payment-domain metrics (FR-012, US3 SC-006). Emitted on the
/// <c>HOAManagementCompany.Payments</c> meter, which the OTel pipeline subscribes to via
/// <c>AddMeter</c>. Counters only — dimensions are low-cardinality (status/outcome/channel) so they
/// never carry PII or unbounded values.
/// </summary>
public sealed class PaymentMetrics : IDisposable
{
    public const string MeterName = "HOAManagementCompany.Payments";

    private readonly Meter _meter;
    private readonly Counter<long> _paymentProcessed;
    private readonly Counter<long> _webhookProcessed;
    private readonly Counter<long> _alertSent;

    public PaymentMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        _paymentProcessed = _meter.CreateCounter<long>(
            "payment.processed", description: "Payment transactions reaching a terminal status.");
        _webhookProcessed = _meter.CreateCounter<long>(
            "webhook.processed", description: "Stripe webhook events handled, by type and outcome.");
        _alertSent = _meter.CreateCounter<long>(
            "alert.sent", description: "Failure-alert delivery attempts, by channel and success.");
    }

    /// <summary>Records a transaction reaching a terminal status (succeeded/failed/returned/…).</summary>
    public void RecordPaymentProcessed(string status) =>
        _paymentProcessed.Add(1, new KeyValuePair<string, object?>("status", status));

    /// <summary>Records a handled webhook (outcome: processed / duplicate / failed / invalid_signature).</summary>
    public void RecordWebhookProcessed(string type, string outcome) =>
        _webhookProcessed.Add(1,
            new KeyValuePair<string, object?>("type", type),
            new KeyValuePair<string, object?>("outcome", outcome));

    /// <summary>Records an alert delivery attempt and whether the provider accepted it.</summary>
    public void RecordAlertSent(string channel, bool success) =>
        _alertSent.Add(1,
            new KeyValuePair<string, object?>("channel", channel),
            new KeyValuePair<string, object?>("success", success));

    public void Dispose() => _meter.Dispose();
}
