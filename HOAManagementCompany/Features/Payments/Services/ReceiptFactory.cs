using System.Text.Json;
using HOAManagementCompany.Domain.Entities;

namespace HOAManagementCompany.Features.Payments.Services;

/// <summary>Builds a durable <see cref="Receipt"/> snapshot for a settled transaction (FR-007f).</summary>
public static class ReceiptFactory
{
    /// <summary>Human-facing confirmation number, e.g. <c>CONF-1A2B3C4D5E6F</c>.</summary>
    public static string NewConfirmationNumber() =>
        $"CONF-{Guid.NewGuid():N}"[..15].ToUpperInvariant();

    /// <summary>Masked method label, e.g. <c>Visa •• 4242</c> or <c>ACH</c>.</summary>
    public static string MaskMethod(string? cardBrand, string? last4) =>
        last4 is not null
            ? $"{Capitalize(cardBrand) ?? "Card"} •• {last4}"
            : "ACH";

    public static Receipt Create(PaymentTransaction txn, string? cardBrand = null, string? last4 = null) => new()
    {
        TransactionId = txn.Id,
        OwnerId = txn.OwnerId,
        ConfirmationNumber = NewConfirmationNumber(),
        MaskedMethod = MaskMethod(cardBrand, last4),
        GrossAmount = txn.GrossAmount,
        FeeAmount = txn.FeeAmount,
        Total = txn.Total,
        RenderModel = JsonSerializer.Serialize(new
        {
            txn.GrossAmount,
            txn.FeeAmount,
            txn.Total,
            method = MaskMethod(cardBrand, last4),
        }),
    };

    private static string? Capitalize(string? s) =>
        string.IsNullOrEmpty(s) ? null : char.ToUpperInvariant(s[0]) + s[1..];
}
