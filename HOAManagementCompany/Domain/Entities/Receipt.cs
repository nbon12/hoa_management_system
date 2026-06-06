namespace HOAManagementCompany.Domain.Entities;

/// <summary>
/// Durable, retrievable payment receipt (FR-007f). Issued at card success / ACH settlement and
/// rendered on demand from <see cref="RenderModel"/>.
/// </summary>
public class Receipt
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid OwnerId { get; set; }

    public string ConfirmationNumber { get; set; } = string.Empty;

    /// <summary>e.g. <c>Visa •• 4242</c>.</summary>
    public string MaskedMethod { get; set; } = string.Empty;

    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal Total { get; set; }

    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>JSON used to render the PDF/HTML on demand.</summary>
    public string RenderModel { get; set; } = "{}";

    public PaymentTransaction Transaction { get; set; } = null!;
}
