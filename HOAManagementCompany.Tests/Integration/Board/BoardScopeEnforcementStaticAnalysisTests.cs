using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// T061 / SC-003 — "No board-scoped endpoint implements its own scope check; every one
/// resolves through the shared primitive, verified by static analysis." This reads the
/// board feature sources off disk (walking up from the test binary to the repo layout) and
/// requires every <c>*Endpoint.cs</c> to call <see cref="HOAManagementCompany.Features.Board.ICommunityScopeResolver"/>
/// unless it is on the documented allow-list below. Plain, fast, no database.
/// </summary>
public class BoardScopeEnforcementStaticAnalysisTests
{
    // The one method every community-scoped endpoint must authorize through (FR-012).
    private const string ResolverCall = "CanAccessAsync";

    // The ONLY endpoints exempt from the resolver gate, each for a documented reason:
    //  • MyCommunitiesEndpoint  — deliberately self-scoped to the caller's own memberships
    //                             (the documented cross-community exception, FR-025).
    //  • BoardModeEndpoint      — an auth/mode endpoint, not a community-scoped resource;
    //                             its server-side board-eligibility check lives in
    //                             AuthService.SwitchModeAsync (FR-014/FR-020).
    private static readonly string[] AllowList =
    [
        "MyCommunitiesEndpoint.cs",
        "BoardModeEndpoint.cs",
    ];

    [Fact]
    public void EveryBoardScopedEndpoint_AuthorizesThroughTheSharedResolver()
    {
        var boardDir = LocateBoardFeatureDirectory();

        var endpoints = boardDir
            .GetFiles("*Endpoint.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        // Guard against a silently-passing scan (wrong directory, renamed suffix).
        Assert.NotEmpty(endpoints);

        // The allow-list must name real files, so a rename cannot quietly widen the
        // exemption to a file that no longer exists.
        var names = endpoints.Select(f => f.Name).ToList();
        foreach (var allowed in AllowList)
            Assert.Contains(allowed, names);

        var offenders = endpoints
            .Where(f => !AllowList.Contains(f.Name, StringComparer.Ordinal))
            .Where(f => !File.ReadAllText(f.FullName).Contains(ResolverCall, StringComparison.Ordinal))
            .Select(f => f.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"SC-003: every board-scoped endpoint must authorize through ICommunityScopeResolver.{ResolverCall}. "
            + $"Offending file(s) in {boardDir.FullName}: {string.Join(", ", offenders)}. "
            + "If an endpoint is genuinely not community-scoped, document why and add it to the "
            + $"allow-list in {nameof(BoardScopeEnforcementStaticAnalysisTests)}.");
    }

    // Walks up from the test binary (bin/<cfg>/<tfm>) until the repo's source layout appears.
    private static DirectoryInfo LocateBoardFeatureDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = new DirectoryInfo(
                Path.Combine(dir.FullName, "HOAManagementCompany", "Features", "Board"));
            if (candidate.Exists)
                return candidate;
        }

        throw new DirectoryNotFoundException(
            "SC-003 static analysis could not locate HOAManagementCompany/Features/Board by walking up from "
            + $"{AppContext.BaseDirectory}.");
    }
}
