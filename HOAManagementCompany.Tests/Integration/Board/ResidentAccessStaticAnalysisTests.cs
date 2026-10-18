using System.Text.RegularExpressions;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// 030 T036 (research R3) — every resident endpoint re-checks the resident link and the community
/// status through <c>ResidentAccess</c> on each request (FR-024g). The rule is positive: every
/// endpoint under the resident folders must use the check (or, for spec 029's slice, go through
/// <c>ResidentArcScope</c>, which must itself use it), unless it is on the allow-list with a reason.
/// No feature code may parse an identity claim with <c>Guid.Parse(User.FindFirst(…))</c>.
/// </summary>
public class ResidentAccessStaticAnalysisTests
{
    private static readonly string[] ResidentFolders = ["Payments", "Property", "Community", "Dashboard"];

    // Endpoints in the resident folders that are not resident endpoints:
    //  • ReconcileEndpoint, RunDraftsEndpoint — secret-authenticated scheduler jobs (006).
    //  • StripeWebhookEndpoint — signature-authenticated processor callbacks (006); archived
    //    communities keep receiving them (030 FR-012).
    private static readonly string[] AllowList = ["ReconcileEndpoint.cs", "RunDraftsEndpoint.cs", "StripeWebhookEndpoint.cs"];

    private static readonly Regex ClaimRead = new(@"FindFirst\(""(propertyId|communityId)""\)|RequirePropertyId\(\)|RequireCommunityId\(\)");

    [Fact]
    public void EveryResidentEndpoint_ChecksAccessOnEveryRequest()
    {
        var features = BoardScopeEnforcementStaticAnalysisTests.LocateFeaturesDirectory();
        var endpoints = ResidentFolders
            .Select(f => new DirectoryInfo(Path.Combine(features.FullName, f)))
            .SelectMany(d => d.GetFiles("*Endpoint.cs", SearchOption.AllDirectories)
                .Concat(d.GetFiles("*Endpoints.cs", SearchOption.AllDirectories)))
            .Where(f => !AllowList.Contains(f.Name, StringComparer.Ordinal))
            .ToList();
        Assert.True(endpoints.Count >= 30, $"expected the resident endpoints; found {endpoints.Count}");

        var offenders = endpoints
            .Where(f => File.ReadAllText(f.FullName) is var text
                        && !(text.Contains("access.RequireAsync(User, ResidentOperation.") || UsesResidentArcScope(f, text)))
            .Select(f => f.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "FR-024g: resident endpoints must call ResidentAccess.RequireAsync (or go through ResidentArcScope). "
            + $"Offending file(s): {string.Join(", ", offenders)}.");
    }

    [Fact]
    public void ResidentEndpoints_NeverReadTheHomeFromTheTokenThemselves()
    {
        var features = BoardScopeEnforcementStaticAnalysisTests.LocateFeaturesDirectory();
        var offenders = ResidentFolders
            .Select(f => new DirectoryInfo(Path.Combine(features.FullName, f)))
            .SelectMany(d => d.GetFiles("*.cs", SearchOption.AllDirectories))
            .Where(f => ClaimRead.IsMatch(File.ReadAllText(f.FullName)))
            .Select(f => f.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "FR-012a: resident code takes the property and community from ResidentAccess, never the token. "
            + $"Offending file(s): {string.Join(", ", offenders)}.");
    }

    [Fact]
    public void NoFeatureCode_ParsesAClaimUnchecked()
    {
        var features = BoardScopeEnforcementStaticAnalysisTests.LocateFeaturesDirectory();
        var offenders = features.GetFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f.FullName).Contains("Guid.Parse(User.FindFirst(", StringComparison.Ordinal))
            .Select(f => f.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    // Spec 029's endpoints authorize through ResidentArcScope, which must use ResidentAccess.
    private static bool UsesResidentArcScope(FileInfo endpoint, string text)
    {
        if (!endpoint.FullName.Contains($"{Path.DirectorySeparatorChar}Architectural{Path.DirectorySeparatorChar}"))
            return false;
        var scope = File.ReadAllText(Path.Combine(endpoint.DirectoryName!, "ResidentArcScope.cs"));
        return scope.Contains("access.RequireAsync(user, operation, ct)")
            && Regex.IsMatch(text, @"\b(drafts|actions|queries|submit)\.\w+Async\(User\b");
    }
}
