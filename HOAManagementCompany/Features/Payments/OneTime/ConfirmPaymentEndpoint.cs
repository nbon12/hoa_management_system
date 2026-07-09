using HOAManagementCompany.Features.Common;
using System.Globalization;
using FastEndpoints;
using FluentValidation;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Ledger;
using HOAManagementCompany.Features.Payments.Services;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Features.Payments.OneTime;

/// <summary>
/// POST /payments/one-time/confirm — records the immutable <see cref="PaymentTransaction"/> for a
/// confirmed PaymentIntent and, for a settled card charge, writes the ledger payment + receipt in
/// one transaction. Gross/fee come from the PaymentIntent metadata (server-authoritative, FR-007b),
/// never from the client. Replaying the same PaymentIntent collapses to the original record (FR-017).
/// ACH stays Pending — its ledger entry is deferred to the settlement webhook (FR-007 ACH).
/// </summary>
public class ConfirmPaymentEndpoint(
    IStripeGateway gateway, ApplicationDbContext db, LedgerService ledger, IdempotencyService idempotency,
    PaymentRecorder recorder)
    : Endpoint<ConfirmPaymentRequest, ConfirmPaymentResponse>
{
    public override void Configure()
    {
        Post("/payments/one-time/confirm");
        Description(x => x.WithName("ConfirmOneTimePayment").WithTags("Payments").RequireRateLimiting("payments"));
    }

    public override async Task HandleAsync(ConfirmPaymentRequest req, CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();
        var idempotencyKey = HttpContext.Request.Headers[IdempotencyService.HeaderName].FirstOrDefault();

        // Replay collapse (FR-007a/FR-035): a re-submitted confirm — keyed by the client idempotency
        // header or by the PaymentIntent itself — returns the original transaction unchanged.
        var byKey = await idempotency.FindExistingAsync(propertyId, idempotencyKey, ct);
        var existing = byKey is not null
            ? await db.PaymentTransactions.Include(t => t.Receipt).FirstAsync(t => t.Id == byKey.Id, ct)
            : await db.PaymentTransactions.Include(t => t.Receipt)
                .FirstOrDefaultAsync(t => t.StripePaymentIntentId == req.PaymentIntentId && t.PropertyId == propertyId, ct);
        if (existing is not null) { await SendOkAsync(Map(existing), ct); return; }

        var pi = await gateway.GetPaymentIntentAsync(req.PaymentIntentId, ct);

        // Server-authoritative amounts: trust the metadata we stamped at intent creation, not the client.
        if (pi.Metadata is null
            || !pi.Metadata.TryGetValue("propertyId", out var metaProperty)
            || !Guid.TryParse(metaProperty, out var intentProperty)
            || intentProperty != propertyId)
        {
            await SendForbiddenAsync(ct);
            return;
        }

        var gross = ParseDecimal(pi.Metadata, "grossAmount");
        var fee = ParseDecimal(pi.Metadata, "feeAmount");
        var method = pi.Metadata.TryGetValue("method", out var m) && m.Equals(nameof(PaymentMethod.Ach), StringComparison.OrdinalIgnoreCase)
            ? PaymentMethod.Ach
            : PaymentMethod.Card;

        var ownerId = await db.Owners.Where(o => o.PropertyId == propertyId).Select(o => o.Id).FirstOrDefaultAsync(ct);

        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            OwnerId = ownerId,
            StripePaymentIntentId = pi.Id,
            StripeChargeId = pi.LatestChargeId,
            GrossAmount = gross,
            FeeAmount = fee,
            Total = gross + fee,
            Currency = pi.Currency,
            Status = MapStatus(pi.Status),
            PaymentMethod = method,
            CardFunding = pi.CardFunding,
            FailureCode = pi.FailureCode,
            FailureMessage = pi.FailureMessage,
            IdempotencyKey = HttpContext.Request.Headers[IdempotencyService.HeaderName].FirstOrDefault(),
        };

        Receipt? receipt = null;
        // One atomic unit via the shared recorder (015 FR-003): txn + ledger payment + receipt
        // commit together under the retrying execution strategy.
        await recorder.RecordNewAsync(txn, async innerCt =>
        {
            receipt = null;   // reset on retry so a partial first attempt isn't double-counted

            // A card charge is settled at confirm → write the ledger payment + receipt now. ACH is still
            // processing (Pending): its ledger entry waits for the settlement webhook to avoid double-posting.
            if (txn.Status == TransactionStatus.Succeeded && method == PaymentMethod.Card)
            {
                await ledger.AddPaymentAsync(propertyId, txn.Id, gross,
                    $"Online Payment – Card – {txn.StripeChargeId}", ct: innerCt);
                receipt = ReceiptFactory.Create(txn, pi.CardBrand, pi.Last4);
                db.Receipts.Add(receipt);
            }
        }, ct);
        txn.Receipt = receipt;
        // Audit trail (FR-029): record the financial-record write with non-PII identifiers only —
        // no card/bank data, names, or amounts-as-PII. Serilog ships this to the structured sink.
        Logger.LogInformation(
            "audit payments.one-time.confirm recorded transaction {TransactionId} status {Status} method {Method} property {PropertyId}",
            txn.Id, txn.Status, method, propertyId);
        await SendOkAsync(Map(txn), ct);
    }

    private static ConfirmPaymentResponse Map(PaymentTransaction txn) => new(
        txn.Id,
        txn.Status.ToString(),
        txn.GrossAmount,
        txn.FeeAmount,
        txn.Total,
        txn.Receipt?.MaskedMethod ?? (txn.PaymentMethod == PaymentMethod.Ach ? "ACH" : "Card"),
        txn.Receipt?.ConfirmationNumber,
        txn.Receipt?.Id);

    private static TransactionStatus MapStatus(string stripeStatus) => stripeStatus switch
    {
        "succeeded" => TransactionStatus.Succeeded,
        "processing" => TransactionStatus.Pending,
        _ => TransactionStatus.Failed,
    };

    private static decimal ParseDecimal(IReadOnlyDictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var v) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d
            : 0m;
}

public class ConfirmPaymentValidator : Validator<ConfirmPaymentRequest>
{
    public ConfirmPaymentValidator() => RuleFor(x => x.PaymentIntentId).NotEmpty();
}
