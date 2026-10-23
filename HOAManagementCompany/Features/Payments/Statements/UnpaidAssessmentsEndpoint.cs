using FastEndpoints;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Payments.Statements;

/// <summary>
/// GET /payments/unpaid-assessments — statutory statement of unpaid assessments against the
/// authenticated owner's lot (NC § 47F-3-118). Returns the net amount due as of today, any credit
/// on file, and a per-category breakdown. Property-scoped via the session's <c>propertyId</c> claim.
/// </summary>
public class UnpaidAssessmentsEndpoint(ResidentAccess access, StatementService statementService)
    : EndpointWithoutRequest<UnpaidAssessmentsResponse>
{
    public override void Configure()
    {
        Get("/payments/unpaid-assessments");
        Description(x => x.WithName("GetUnpaidAssessments").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.ReadHistory, ct);
        var propertyId = home.PropertyId;
        var result = await statementService.GetUnpaidAssessmentsAsync(propertyId, ct);
        await SendOkAsync(result, ct);
    }
}
