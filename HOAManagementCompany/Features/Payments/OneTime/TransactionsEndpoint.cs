using FastEndpoints;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Features.Payments.OneTime;

/// <summary>GET /payments/transactions — paged payment history for the caller's property (FR-007g).</summary>
public class TransactionsEndpoint(ApplicationDbContext db) : EndpointWithoutRequest<TransactionsResponse>
{
    public override void Configure()
    {
        Get("/payments/transactions");
        Description(x => x.WithName("GetPaymentTransactions").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = Guid.Parse(User.FindFirst("propertyId")!.Value);
        var limit = Math.Clamp(Query<int?>("limit", isRequired: false) ?? 50, 1, 200);
        var offset = Math.Max(0, Query<int?>("offset", isRequired: false) ?? 0);

        var query = db.PaymentTransactions
            .AsNoTracking()
            .Where(t => t.PropertyId == propertyId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .Select(t => new TransactionDto(
                t.Id,
                t.CreatedAt,
                t.GrossAmount,
                t.FeeAmount,
                t.Total,
                t.CumulativeRefundedAmount,
                t.Status.ToString(),
                t.PaymentMethod.ToString(),
                t.Receipt != null ? t.Receipt.MaskedMethod : (t.PaymentMethod == PaymentMethod.Ach ? "ACH" : "Card"),
                t.IsRecurring))
            .ToListAsync(ct);

        await SendOkAsync(new TransactionsResponse(items, total, limit, offset), ct);
    }
}
