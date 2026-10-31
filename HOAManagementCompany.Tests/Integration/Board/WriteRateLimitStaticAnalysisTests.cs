using System.Text.RegularExpressions;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// 030 T025 — FR-041a/b: every write endpoint on the board, company and invitation surfaces carries
/// a rate limit. Board and company writes use the per-user <c>board-writes</c> policy; invitation
/// endpoints, reachable before sign-in, use the per-client <c>auth</c> policy. Static and fast.
/// </summary>
public class WriteRateLimitStaticAnalysisTests
{
    private static readonly Regex WriteVerb = new(@"^\s*(Post|Put|Patch|Delete)\(", RegexOptions.Multiline);

    // Writes exempt from the rule, each for a documented reason:
    //  • ArcSweepJobEndpoint — secret-authenticated scheduler job, not a user action (027 R6).
    //  • BoardModeEndpoint   — a sign-in mode switch that rotates tokens; it is an auth action,
    //                          not user-generated content (025 FR-018).
    private static readonly string[] AllowList = ["ArcSweepJobEndpoint.cs", "BoardModeEndpoint.cs"];

    [Theory]
    [InlineData("Board", "board-writes")]
    [InlineData("Company", "board-writes")]
    [InlineData("Invitations", "auth")]
    public void EveryWriteEndpoint_RequiresTheFolderRateLimit(string folder, string policy)
    {
        var dir = new DirectoryInfo(Path.Combine(
            BoardScopeEnforcementStaticAnalysisTests.LocateFeaturesDirectory().FullName, folder));
        if (!dir.Exists)
            return; // The folder arrives with its first endpoint; nothing to check before then.

        var writes = dir.GetFiles("*Endpoint.cs", SearchOption.AllDirectories)
            .Concat(dir.GetFiles("*Endpoints.cs", SearchOption.AllDirectories))
            .Where(f => !AllowList.Contains(f.Name, StringComparer.Ordinal))
            .Where(f => WriteVerb.IsMatch(File.ReadAllText(f.FullName)))
            .ToList();

        var offenders = writes
            .Where(f => !File.ReadAllText(f.FullName).Contains($"RequireRateLimiting(\"{policy}\")", StringComparison.Ordinal))
            .Select(f => f.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"FR-041: every write endpoint under Features/{folder} must call RequireRateLimiting(\"{policy}\"). "
            + $"Offending file(s): {string.Join(", ", offenders)}.");
    }

    // 006 FR-028: the payment-method setup endpoints (both call Stripe) use the per-client payments policy.
    [Theory]
    [InlineData("Payments/Recurring/SetupIntentEndpoint.cs")]
    [InlineData("Payments/Recurring/RecurringUpsertEndpoint.cs")]
    public void PaymentSetupEndpoints_RequireThePaymentsRateLimit(string file)
    {
        var path = Path.Combine(BoardScopeEnforcementStaticAnalysisTests.LocateFeaturesDirectory().FullName, file);
        Assert.Contains("RequireRateLimiting(\"payments\")", File.ReadAllText(path), StringComparison.Ordinal);
    }
}
