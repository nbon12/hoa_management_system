using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Features.Payments.Ledger;
using HOAManagementCompany.Features.Payments.Services;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Payments.OneTime;

/// <summary>GET /payments/options — balance, next assessment, credit balance, and fee policy (FR-007).</summary>
public class PaymentOptionsEndpoint(LedgerService ledger, PaymentConfigService config, ApplicationDbContext db)
    : EndpointWithoutRequest<PaymentOptionsResponse>
{
    public override void Configure()
    {
        Get("/payments/options");
        Description(x => x.WithName("GetPaymentOptions").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = User.GetPropertyId();

        var property = await db.Properties
            .Where(p => p.Id == propertyId)
            .Select(p => new { p.MonthlyAssessment, p.AssessmentDueDay })
            .FirstOrDefaultAsync(ct);
        if (property is null) { await SendNotFoundAsync(ct); return; }

        var balance = await ledger.GetCurrentBalanceAsync(propertyId, ct);
        var cfg = await config.GetForPropertyAsync(propertyId, ct);

        await SendOkAsync(new PaymentOptionsResponse(
            CurrentBalance: Math.Max(0m, balance),
            CreditBalance: balance < 0m ? -balance : 0m,
            NextAssessment: property.MonthlyAssessment,
            NextAssessmentDueDate: NextDueDate(property.AssessmentDueDay),
            CardFeeType: cfg.CardFeeType.ToString(),
            CardFeeValue: cfg.CardFeeValue,
            CardScope: cfg.CardScope.ToString(),
            SurchargingEnabled: cfg.SurchargingEnabled,
            AchFeeValue: cfg.AchFeeValue), ct);
    }

    private static DateOnly? NextDueDate(int dueDay)
    {
        if (dueDay is < 1 or > 31) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var day = Math.Min(dueDay, DateTime.DaysInMonth(today.Year, today.Month));
        var candidate = new DateOnly(today.Year, today.Month, day);
        if (candidate >= today) return candidate;
        var next = today.AddMonths(1);
        return new DateOnly(next.Year, next.Month, Math.Min(dueDay, DateTime.DaysInMonth(next.Year, next.Month)));
    }
}
