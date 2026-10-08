using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Architectural;

/// <summary>
/// T014 — the pure decision function behind 027 FR-023/FR-013: both decision rules across board
/// sizes 3–6, the early "can't be overtaken" win, the due-date branch with and without quorum,
/// and the revisions-vs-deny wording split.
/// </summary>
public class ArcDecisionRulesTheoryTests
{
    private const ArcDecisionRule Members = ArcDecisionRule.MajorityOfMembers;
    private const ArcDecisionRule Cast = ArcDecisionRule.MajorityOfVotesCastWithQuorum;

    // (rule, eligible, approve, revisionsNeeded, deny, dueDatePassed, expectedOutcome, expectedWording)
    public static TheoryData<ArcDecisionRule, int, int, int, int, bool, ArcOutcome?, ArcDenialWording?> Cases => new()
    {
        // ── Majority of members: more than half of eligible on one side ──
        { Members, 3, 1, 0, 0, false, null, null },
        { Members, 3, 2, 0, 0, false, ArcOutcome.Approved, null },
        { Members, 3, 0, 1, 1, false, ArcOutcome.Denied, ArcDenialWording.RevisionsRequested },
        { Members, 4, 2, 0, 2, false, null, null },                       // tie never decides
        { Members, 4, 3, 0, 0, false, ArcOutcome.Approved, null },
        { Members, 4, 0, 0, 3, false, ArcOutcome.Denied, ArcDenialWording.Denied },
        { Members, 5, 2, 0, 0, false, null, null },                       // US6-S1 before the third vote
        { Members, 5, 3, 0, 0, false, ArcOutcome.Approved, null },        // US6-S1
        { Members, 5, 0, 0, 3, false, ArcOutcome.Denied, ArcDenialWording.Denied }, // US6-S2
        { Members, 5, 2, 0, 2, false, null, null },                       // US6-S3
        { Members, 5, 1, 2, 1, false, ArcOutcome.Denied, ArcDenialWording.RevisionsRequested }, // US6-S9
        { Members, 5, 0, 1, 3, false, ArcOutcome.Denied, ArcDenialWording.Denied },  // US6-S11 wording
        { Members, 5, 2, 0, 1, true, null, null },                        // due date doesn't decide under this rule
        { Members, 6, 3, 0, 3, false, null, null },
        { Members, 6, 4, 0, 0, false, ArcOutcome.Approved, null },
        { Members, 6, 0, 2, 2, false, ArcOutcome.Denied, ArcDenialWording.RevisionsRequested }, // RN == Deny → RevisionsRequested

        // ── Majority of votes cast with a quorum (> half of eligible have voted) ──
        { Cast, 5, 3, 0, 0, false, ArcOutcome.Approved, null },           // US6-S4: 2 remaining can't overtake
        { Cast, 5, 2, 0, 1, false, null, null },                          // quorum met but 2 remaining could overtake
        { Cast, 5, 2, 0, 1, true, ArcOutcome.Approved, null },            // US6-S5: leader at due date
        { Cast, 5, 1, 0, 1, true, null, null },                           // US6-S6: no quorum → lapse rule
        { Cast, 5, 1, 1, 1, true, ArcOutcome.Denied, ArcDenialWording.RevisionsRequested },
        { Cast, 4, 1, 0, 1, true, null, null },                           // no quorum (needs 3 of 4)
        { Cast, 4, 2, 0, 1, true, ArcOutcome.Approved, null },
        { Cast, 4, 2, 0, 2, true, null, null },                           // tie at due date → lapse rule
        { Cast, 3, 2, 0, 0, false, ArcOutcome.Approved, null },           // 1 remaining can't overtake 2–0
        { Cast, 6, 3, 0, 1, false, null, null },                          // 2 remaining could tie
        { Cast, 6, 4, 0, 1, false, ArcOutcome.Approved, null },
        { Cast, 6, 0, 3, 1, true, ArcOutcome.Denied, ArcDenialWording.RevisionsRequested },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Evaluate_ReturnsTheExpectedDecision(
        ArcDecisionRule rule, int eligible, int approve, int revisionsNeeded, int deny, bool dueDatePassed,
        ArcOutcome? expectedOutcome, ArcDenialWording? expectedWording)
    {
        var decision = ArcDecisionRules.Evaluate(rule, eligible, approve, revisionsNeeded, deny, dueDatePassed);

        Assert.Equal(expectedOutcome, decision?.Outcome);
        Assert.Equal(expectedWording, decision?.Wording);
    }

    [Theory]
    [InlineData(Members, 5, "Three of five votes decide. The manager records the outcome and notifies the owner.")]
    [InlineData(Members, 4, "Three of four votes decide. The manager records the outcome and notifies the owner.")]
    [InlineData(Members, 3, "Two of three votes decide. The manager records the outcome and notifies the owner.")]
    [InlineData(Members, 6, "Four of six votes decide. The manager records the outcome and notifies the owner.")]
    [InlineData(Cast, 5, "A majority of votes cast decides once three of five members have voted. The manager records the outcome and notifies the owner.")]
    [InlineData(Cast, 4, "A majority of votes cast decides once three of four members have voted. The manager records the outcome and notifies the owner.")]
    public void RuleText_ReflectsRuleAndBoardSize(ArcDecisionRule rule, int eligible, string expected) =>
        Assert.Equal(expected, ArcDecisionRules.RuleText(rule, eligible));

    [Fact]
    public void Evaluate_WithNoEligibleMembers_NeverDecides() =>
        Assert.Null(ArcDecisionRules.Evaluate(Members, 0, 0, 0, 0, dueDatePassed: true));

    [Fact]
    public void Evaluate_CountsVotesFromMembersWhoLeft_WithoutNegativeRemaining()
    {
        // 3 eligible now, but 4 votes on record (one voter has since left the board):
        // remaining can't go negative and the 3 approvals still decide.
        var d = ArcDecisionRules.Evaluate(Cast, 3, 3, 0, 1, dueDatePassed: false);
        Assert.Equal(ArcOutcome.Approved, d?.Outcome);
    }
}
