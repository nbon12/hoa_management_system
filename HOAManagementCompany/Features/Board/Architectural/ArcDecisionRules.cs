using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>A reached decision: the legal outcome, plus the owner-facing wording when denied.</summary>
public sealed record ArcDecision(ArcOutcome Outcome, ArcDenialWording? Wording);

// <!-- REPOWISE:START domain=arc-decision -->
// ArcDecisionRules: the pure decision logic for architectural applications (027 FR-023,
// FR-013, research R4). Votes form two sides: approve, and denial (RevisionsNeeded + Deny).
// "Majority of members" decides once a side holds more than half of eligible members.
// "Majority of votes cast with a quorum" decides early once quorum is met and the
// remaining eligible votes can't overtake the leader, otherwise at the due date if quorum
// is met and one side leads; no quorum or a tie hands over to the lapse rule. A denial's
// wording is RevisionsRequested when RevisionsNeeded votes are at least as many as Deny.
// <!-- REPOWISE:END -->

public static class ArcDecisionRules
{
    private const string OutcomeSentence = "The manager records the outcome and notifies the owner.";

    public static ArcDecision? Evaluate(
        ArcDecisionRule rule, int eligible, int approve, int revisionsNeeded, int deny, bool dueDatePassed)
    {
        if (eligible <= 0)
            return null;

        var denial = revisionsNeeded + deny;
        var majority = eligible / 2 + 1;

        bool? approveWins = rule switch
        {
            ArcDecisionRule.MajorityOfMembers =>
                approve >= majority ? true : denial >= majority ? false : null,
            ArcDecisionRule.MajorityOfVotesCastWithQuorum =>
                VotesCastWinner(eligible, majority, approve, denial, dueDatePassed),
            _ => null
        };

        return approveWins switch
        {
            true => new ArcDecision(ArcOutcome.Approved, null),
            false => new ArcDecision(ArcOutcome.Denied, DenialWording(revisionsNeeded, deny)),
            null => null
        };
    }

    public static ArcDenialWording DenialWording(int revisionsNeeded, int deny) =>
        revisionsNeeded >= deny ? ArcDenialWording.RevisionsRequested : ArcDenialWording.Denied;

    public static string RuleText(ArcDecisionRule rule, int eligible)
    {
        if (eligible <= 0)
            return $"No board members are eligible to vote. {OutcomeSentence}";

        var majority = Words(eligible / 2 + 1);
        var size = Words(eligible);
        return rule == ArcDecisionRule.MajorityOfVotesCastWithQuorum
            ? $"A majority of votes cast decides once {majority} of {size} members have voted. {OutcomeSentence}"
            : $"{Capitalize(majority)} of {size} votes decide. {OutcomeSentence}";
    }

    private static bool? VotesCastWinner(int eligible, int quorum, int approve, int denial, bool dueDatePassed)
    {
        var cast = approve + denial;
        if (cast < quorum)
            return null;

        // Votes from members who have since left still count, so remaining never goes negative.
        var remaining = Math.Max(0, eligible - cast);
        if (approve > denial + remaining) return true;
        if (denial > approve + remaining) return false;

        if (dueDatePassed && approve != denial)
            return approve > denial;
        return null;
    }

    private static readonly string[] Numbers =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen",
        "nineteen", "twenty"
    ];

    private static string Words(int n) => n < Numbers.Length ? Numbers[n] : n.ToString();

    private static string Capitalize(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
