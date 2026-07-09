using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Features.Community.Models;

namespace HOAManagementCompany.Features.Community.Documents;

public class DocumentsEndpoint(CommunityService communityService) : Endpoint<DocumentListRequest, DocumentListResponse>
{
    public override void Configure()
    {
        Get("/community/documents");
        Description(x => x.WithName("GetDocuments").WithTags("Community"));
    }

    public override async Task HandleAsync(DocumentListRequest req, CancellationToken ct)
    {
        var communityId = User.GetCommunityId();
        await SendOkAsync(await communityService.GetDocumentsAsync(communityId, req, ct), ct);
    }
}
