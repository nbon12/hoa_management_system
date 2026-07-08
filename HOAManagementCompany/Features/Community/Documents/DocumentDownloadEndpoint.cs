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
        var communityId = User.FindFirst("communityId")!.Value;
        var id = Route<Guid>("id");

        try { await SendOkAsync(await communityService.GetDocumentDownloadUrlAsync(communityId, id, ct), ct); }
        catch (DomainException ex) { HttpContext.Response.StatusCode = ex.StatusCode;
        await HttpContext.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct); }
    }
}
