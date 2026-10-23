using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Features.Board;

namespace HOAManagementCompany.Features.Company;

public sealed class LifecyclePreviewRequest
{
    public Guid CommunityId { get; set; }
    public string Action { get; set; } = string.Empty;
}

public sealed class SetParentRequest
{
    public Guid CommunityId { get; set; }
    public Guid? ParentCommunityId { get; set; }
}

// 030 contracts/company.md § Lifecycle: the status actions and the parent link, Company
// Administrators only (ManageCommunityLifecycle). The status actions take no body. The capability is allowed while archived, so
// restore and its preview work there; activate, archive and parent then refuse with the right code.

/// <summary>GET /communities/{communityId}/lifecycle/{action}/preview — what the action would do.</summary>
public class LifecyclePreviewEndpoint(ICommunityScopeResolver scope, CommunityLifecycleService lifecycle)
    : Endpoint<LifecyclePreviewRequest, LifecyclePreview>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/lifecycle/{action}/preview");
        Description(x => x.WithName("LifecyclePreview").WithTags("Company"));
    }

    public override async Task HandleAsync(LifecyclePreviewRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await BoardHttp.SendDecisionAsync(HttpContext, await scope.DecideAsync(User, req.CommunityId,
                CommunityCapability.ManageCommunityLifecycle, ScopeOperation.Read, ct), ct))
            return;
        await SendOkAsync(await lifecycle.PreviewAsync(req.CommunityId, req.Action.ToLowerInvariant(), ct), ct);
    }
}

/// <summary>POST /communities/{communityId}/activate.</summary>
public class LifecycleActivateEndpoint(ICommunityScopeResolver scope, CommunityLifecycleService lifecycle)
    : EndpointWithoutRequest<ActivateResult>
{
    public override void Configure()
    {
        Post("/communities/{communityId}/activate");
        Description(x => x.WithName("ActivateCommunity").WithTags("Company").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var communityId = Route<Guid>("communityId");
        if (await LifecycleHttp.RefuseAsync(this, scope, communityId, ct))
            return;
        await SendOkAsync(await lifecycle.ActivateAsync(communityId, LifecycleHttp.Actor(User), ct), ct);
    }
}

/// <summary>POST /communities/{communityId}/archive.</summary>
public class LifecycleArchiveEndpoint(ICommunityScopeResolver scope, CommunityLifecycleService lifecycle)
    : EndpointWithoutRequest<ArchiveResult>
{
    public override void Configure()
    {
        Post("/communities/{communityId}/archive");
        Description(x => x.WithName("ArchiveCommunity").WithTags("Company").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var communityId = Route<Guid>("communityId");
        if (await LifecycleHttp.RefuseAsync(this, scope, communityId, ct))
            return;
        await SendOkAsync(await lifecycle.ArchiveAsync(communityId, LifecycleHttp.Actor(User), ct), ct);
    }
}

/// <summary>POST /communities/{communityId}/restore.</summary>
public class LifecycleRestoreEndpoint(ICommunityScopeResolver scope, CommunityLifecycleService lifecycle)
    : EndpointWithoutRequest<RestoreResult>
{
    public override void Configure()
    {
        Post("/communities/{communityId}/restore");
        Description(x => x.WithName("RestoreCommunity").WithTags("Company").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var communityId = Route<Guid>("communityId");
        if (await LifecycleHttp.RefuseAsync(this, scope, communityId, ct))
            return;
        await SendOkAsync(await lifecycle.RestoreAsync(communityId, LifecycleHttp.Actor(User), ct), ct);
    }
}

/// <summary>PUT /communities/{communityId}/parent.</summary>
public class LifecycleParentEndpoint(ICommunityScopeResolver scope, CommunityLifecycleService lifecycle)
    : Endpoint<SetParentRequest>
{
    public override void Configure()
    {
        Put("/communities/{communityId}/parent");
        Description(x => x.WithName("SetCommunityParent").WithTags("Company").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(SetParentRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await LifecycleHttp.RefuseAsync(this, scope, req.CommunityId, ct))
            return;
        await lifecycle.SetParentAsync(req.CommunityId, req.ParentCommunityId, LifecycleHttp.Actor(User), ct);
        await SendOkAsync(new { parentCommunityId = req.ParentCommunityId }, ct);
    }
}

internal static class LifecycleHttp
{
    public static async Task<bool> RefuseAsync(IEndpoint endpoint, ICommunityScopeResolver scope, Guid communityId, CancellationToken ct) =>
        await BoardHttp.SendDecisionAsync(endpoint.HttpContext, await scope.DecideAsync(endpoint.HttpContext.User, communityId,
            CommunityCapability.ManageCommunityLifecycle, ScopeOperation.Write, ct), ct);

    public static string Actor(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")!.Value;
}
