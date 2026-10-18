using FastEndpoints;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Community.Models;
using HOAManagementCompany.Features.Residents;

namespace HOAManagementCompany.Features.Community.Documents;

public class DocumentsEndpoint(ResidentAccess access, CommunityService communityService) : Endpoint<DocumentListRequest, DocumentListResponse>
{
    public override void Configure()
    {
        Get("/community/documents");
        Description(x => x.WithName("GetDocuments").WithTags("Community"));
    }

    public override async Task HandleAsync(DocumentListRequest req, CancellationToken ct)
    {
        // 030 FR-024g: re-check the resident link and the community status on every request.
        var home = await access.RequireAsync(User, ResidentOperation.Read, ct);
        var communityId = home.CommunityId;
        await SendOkAsync(await communityService.GetDocumentsAsync(communityId, req, ct), ct);
    }
}
