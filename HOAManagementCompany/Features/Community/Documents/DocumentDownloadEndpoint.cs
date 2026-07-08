using HOAManagementCompany.Features.Common;
using FastEndpoints;
using HOAManagementCompany.Domain;
using HOAManagementCompany.Features.Community.Models;

namespace HOAManagementCompany.Features.Community.Documents;

public class DocumentDownloadEndpoint(CommunityService communityService) : EndpointWithoutRequest<DocumentDownloadResponse>
{
    public override void Configure()
    {
        Get("/community/documents/{id}/download");
        Description(x => x.WithName("GetDocumentDownload").WithTags("Community"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var communityId = User.GetCommunityId();
        var id = Route<Guid>("id");

        await SendOkAsync(await communityService.GetDocumentDownloadUrlAsync(communityId, id, ct), ct);
    }
}
