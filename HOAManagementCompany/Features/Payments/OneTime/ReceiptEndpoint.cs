using FastEndpoints;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Payments.OneTime;

/// <summary>GET /payments/receipts/{id} — retrieves a durable receipt, scoped to the caller's property (FR-007f).</summary>
public class ReceiptEndpoint(ResidentAccess access, ApplicationDbContext db) : EndpointWithoutRequest<ReceiptResponse>
{
    public override void Configure()
    {
        Get("/payments/receipts/{id}");
        Description(x => x.WithName("GetReceipt").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.ReadHistory, ct);
        var propertyId = home.PropertyId;
        var id = Route<Guid>("id");

        var receipt = await db.Receipts
            .AsNoTracking()
            .Where(r => r.Id == id && r.Transaction.PropertyId == propertyId)
            .Select(r => new ReceiptResponse(
                r.Id, r.TransactionId, r.ConfirmationNumber, r.MaskedMethod,
                r.GrossAmount, r.FeeAmount, r.Total, r.IssuedAt))
            .FirstOrDefaultAsync(ct);

        if (receipt is null) { await SendNotFoundAsync(ct); return; }
        await SendOkAsync(receipt, ct);
    }
}
