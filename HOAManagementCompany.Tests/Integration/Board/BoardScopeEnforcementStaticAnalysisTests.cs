using System.Text.RegularExpressions;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// T061 / SC-003 — "No board-scoped endpoint implements its own scope check; every one
/// resolves through the shared primitive, verified by static analysis." This reads the
/// feature sources off disk (walking up from the test binary to the repo layout) and
/// requires every endpoint file to call <see cref="HOAManagementCompany.Features.Board.ICommunityScopeResolver"/>
/// unless it is on the documented allow-list below. Plain, fast, no database.
/// <para>
/// 030 R3 widened the scan to <c>Features/Company</c> and <c>Features/Residents</c>, to plural
/// <c>*Endpoints.cs</c> files, and to the resolver's 030 methods. <c>Features/Invitations</c> is
/// deliberately not scanned: invitation acceptance is authorized by the link token, not by
/// membership (FR-004), and has its own refusal-order tests.
/// </para>
/// </summary>
public class BoardScopeEnforcementStaticAnalysisTests
{
    // The resolver methods an endpoint may authorize through (025 FR-012, 030 FR-004).
    private static readonly Regex ResolverCall = new(@"\b(CanAccessAsync|DecideAsync|CanAccessCompanyAsync)\b");

    private static readonly string[] ScannedFolders = ["Board", "Company", "Residents"];

    // The ONLY endpoints exempt from the resolver gate, each for a documented reason:
    //  • MyCommunitiesEndpoint  — deliberately self-scoped to the caller's own memberships
    //                             (the documented cross-community exception, FR-025).
    //  • BoardModeEndpoint      — an auth/mode endpoint, not a community-scoped resource;
    //                             its server-side board-eligibility check lives in
    //                             AuthService.SwitchModeAsync (FR-014/FR-020).
    //  • ArcSweepJobEndpoint    — secret-authenticated scheduler job, not a user-facing
    //                             community resource (027 research R6).
    private static readonly string[] AllowList =
    [
        "MyCommunitiesEndpoint.cs",
        "BoardModeEndpoint.cs",
        "ArcSweepJobEndpoint.cs",
    ];

    [Fact]
    public void EveryBoardScopedEndpoint_AuthorizesThroughTheSharedResolver()
    {
        var featuresDir = LocateFeaturesDirectory();

        var endpoints = ScannedFolders
            .Select(f => new DirectoryInfo(Path.Combine(featuresDir.FullName, f)))
            .Where(d => d.Exists)
            .SelectMany(d => d.GetFiles("*Endpoint.cs", SearchOption.AllDirectories)
                .Concat(d.GetFiles("*Endpoints.cs", SearchOption.AllDirectories)))
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
            .Where(f => !ResolverCall.IsMatch(File.ReadAllText(f.FullName)))
            .Select(f => f.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "SC-003: every board-scoped endpoint must authorize through ICommunityScopeResolver "
            + "(CanAccessAsync, DecideAsync or CanAccessCompanyAsync). "
            + $"Offending file(s): {string.Join(", ", offenders)}. "
            + "If an endpoint is genuinely not community-scoped, document why and add it to the "
            + $"allow-list in {nameof(BoardScopeEnforcementStaticAnalysisTests)}.");
    }

    // Walks up from the test binary (bin/<cfg>/<tfm>) until the repo's source layout appears.
    internal static DirectoryInfo LocateFeaturesDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = new DirectoryInfo(Path.Combine(dir.FullName, "HOAManagementCompany", "Features"));
            if (candidate.Exists)
                return candidate;
        }

        throw new DirectoryNotFoundException(
            "Static analysis could not locate HOAManagementCompany/Features by walking up from "
            + $"{AppContext.BaseDirectory}.");
    }
}
