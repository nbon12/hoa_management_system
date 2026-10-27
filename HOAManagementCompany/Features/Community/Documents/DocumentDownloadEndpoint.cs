using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Community.Documents;

public class DocumentDownloadEndpoint(ResidentAccess access, CommunityService communityService) : EndpointWithoutRequest<DocumentDownloadResponse>
{
    public override void Configure()
    {
        Get("/community/documents/{id}/download");
        Description(x => x.WithName("GetDocumentDownload").WithTags("Community"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var communityId = home.CommunityId;
        var id = Route<Guid>("id");

        try { await SendOkAsync(await communityService.GetDocumentDownloadUrlAsync(communityId, id, ct), ct); }
        catch (DomainException ex) { HttpContext.Response.StatusCode = ex.StatusCode;
        await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct); }
    }
}
