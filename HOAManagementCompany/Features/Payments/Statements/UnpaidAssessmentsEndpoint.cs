using FastEndpoints;
using HOAManagementCompany.Features.Payments.Models;

namespace HOAManagementCompany.Features.Payments.Statements;

/// <summary>
/// GET /payments/unpaid-assessments — statutory statement of unpaid assessments against the
/// authenticated owner's lot (NC § 47F-3-118). Returns the net amount due as of today, any credit
/// on file, and a per-category breakdown. Property-scoped via the session's <c>propertyId</c> claim.
/// </summary>
public class UnpaidAssessmentsEndpoint(StatementService statementService)
    : EndpointWithoutRequest<UnpaidAssessmentsResponse>
{
    public override void Configure()
    {
        Get("/payments/unpaid-assessments");
        Description(x => x.WithName("GetUnpaidAssessments").WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var propertyId = Guid.Parse(User.FindFirst("propertyId")!.Value);
        var result = await statementService.GetUnpaidAssessmentsAsync(propertyId, ct);
        await SendOkAsync(result, ct);
    }
}
