using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Company;

/// <summary>
/// 030 T028 — the open-ended-manager rule (FR-020; research R7). A community "has a manager" only
/// while at least one Community Manager membership is active with no end date. A change that would
/// leave a community that has one with none is refused (LAST_MANAGER); a first manager must be
/// open-ended; an Archived community skips the rule.
/// </summary>
public class ManagerRuleTheoryTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static ManagerRule.Row OpenEnded(Guid id) => new(id, MembershipStatus.Active, null);
    private static ManagerRule.Row EndDated(Guid id, int days) => new(id, MembershipStatus.Active, Today.AddDays(days));
    private static ManagerRule.Row Ended(Guid id) => new(id, MembershipStatus.Inactive, Today.AddDays(-3));

    public static IEnumerable<object[]> Cases() =>
    [
        // US2-S9: the only open-ended manager can't be ended, downgraded or deactivated.
        ["only open-ended · end", new[] { OpenEnded(A) }, ManagerRule.Change.End(A), false],
        ["only open-ended · downgrade", new[] { OpenEnded(A) }, ManagerRule.Change.Downgrade(A), false],
        ["only open-ended · deactivate", new[] { OpenEnded(A) }, ManagerRule.Change.Deactivate(A), false],
        // US2-S10: nor given an end date.
        ["only open-ended · set end date", new[] { OpenEnded(A) }, ManagerRule.Change.SetEndDate(A, Today.AddMonths(2)), false],
        // One of two open-ended managers can go.
        ["two open-ended · end one", new[] { OpenEnded(A), OpenEnded(B) }, ManagerRule.Change.End(A), true],
        ["two open-ended · downgrade one", new[] { OpenEnded(A), OpenEnded(B) }, ManagerRule.Change.Downgrade(B), true],
        ["two open-ended · end-date one", new[] { OpenEnded(A), OpenEnded(B) }, ManagerRule.Change.SetEndDate(A, Today.AddDays(30)), true],
        // An end-dated co-manager doesn't count: the open-ended one stays required.
        ["open-ended + end-dated · end the open-ended", new[] { OpenEnded(A), EndDated(B, 30) }, ManagerRule.Change.End(A), false],
        ["open-ended + end-dated · end the end-dated", new[] { OpenEnded(A), EndDated(B, 30) }, ManagerRule.Change.End(B), true],
        ["open-ended + end-dated · move the end date earlier", new[] { OpenEnded(A), EndDated(B, 30) }, ManagerRule.Change.SetEndDate(B, Today.AddDays(5)), true],
        ["end-dated · clear its end date", new[] { OpenEnded(A), EndDated(B, 30) }, ManagerRule.Change.SetEndDate(B, null), true],
        // Appointing: an end-dated appointment needs another open-ended manager to remain.
        ["has open-ended · appoint end-dated", new[] { OpenEnded(A) }, ManagerRule.Change.Appoint(Today.AddMonths(6)), true],
        ["has open-ended · appoint open-ended", new[] { OpenEnded(A) }, ManagerRule.Change.Appoint(null), true],
        ["first manager · open-ended", Array.Empty<ManagerRule.Row>(), ManagerRule.Change.Appoint(null), true],
        ["first manager · end-dated", Array.Empty<ManagerRule.Row>(), ManagerRule.Change.Appoint(Today.AddMonths(6)), false],
        ["only end-dated left · appoint end-dated", new[] { EndDated(A, 10) }, ManagerRule.Change.Appoint(Today.AddMonths(6)), false],
        ["only ended rows · appoint open-ended", new[] { Ended(A) }, ManagerRule.Change.Appoint(null), true],
        // Accepting an invitation is an appointment at acceptance time (FR-022 Terms).
        ["accept open-ended", Array.Empty<ManagerRule.Row>(), ManagerRule.Change.Appoint(null), true],
        ["accept end-dated with no open-ended manager", new[] { EndDated(A, 10) }, ManagerRule.Change.Appoint(Today.AddDays(40)), false],
        // Replace: the replacement is open-ended, then the current one ends (FR-020).
        ["replace the only manager", new[] { OpenEnded(A) }, ManagerRule.Change.Replace(A), true],
        // A community that has no open-ended manager can lose end-dated ones.
        ["no open-ended · end an end-dated", new[] { EndDated(A, 10), EndDated(B, 20) }, ManagerRule.Change.End(A), true],
        ["no managers at all · nothing to protect", Array.Empty<ManagerRule.Row>(), ManagerRule.Change.End(A), true],
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Evaluate_AppliesTheOpenEndedManagerRule(string label, ManagerRule.Row[] rows, ManagerRule.Change change, bool allowed)
    {
        Assert.True(allowed == ManagerRule.Evaluate(rows, change, Today, archived: false), label);
    }

    [Fact]
    public void ArchivedCommunity_SkipsTheRule()
    {
        Assert.True(ManagerRule.Evaluate([OpenEnded(A)], ManagerRule.Change.End(A), Today, archived: true));
    }

    [Fact]
    public void HasManager_CountsOnlyActiveRowsWithNoEndDate()
    {
        Assert.True(ManagerRule.HasOpenEndedManager([OpenEnded(A)]));
        Assert.False(ManagerRule.HasOpenEndedManager([EndDated(A, 100), Ended(B)]));
        Assert.False(ManagerRule.HasOpenEndedManager([]));
    }
}
