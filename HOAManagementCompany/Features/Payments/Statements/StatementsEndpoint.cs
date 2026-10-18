using FastEndpoints;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Payments.Statements;

/// <summary>
/// GET /payments/statements — periodic account statement for the authenticated owner's property
/// (FR-039). Optional <c>startDate</c>/<c>endDate</c> (ISO yyyy-MM-dd) bound the window; it defaults
/// to the trailing 12 months. Property-scoped via the session's <c>propertyId</c> claim.
/// </summary>
public class StatementsEndpoint(ResidentAccess access, StatementService statementService)
    : Endpoint<StatementRequest, StatementResponse>
{
    public override void Configure()
    {
        Get("/payments/statements");
        Description(x => x.WithName("GetStatement").WithTags("Payments"));
    }

    public override async Task HandleAsync(StatementRequest req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.ReadHistory, ct);
        var propertyId = home.PropertyId;
        var result = await statementService.GetStatementAsync(propertyId, req, ct);
        await SendOkAsync(result, ct);
    }
}
