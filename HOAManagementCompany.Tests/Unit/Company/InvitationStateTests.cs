using HOAManagementCompany.Features.Invitations;
using Xunit;
using static HOAManagementCompany.Features.Invitations.InvitationAction;
using static HOAManagementCompany.Features.Invitations.InvitationTransitions.ViewState;

namespace HOAManagementCompany.Tests.Unit.Company;

/// <summary>
/// 030 T045 — the invitation state machine, exactly as data-model.md's transition table (FR-022):
/// every state (Expired is derived from Pending) × every action gives the next state, or the
/// refusal code with the state unchanged.
/// </summary>
public class InvitationStateTests
{
    public static TheoryData<InvitationTransitions.ViewState, InvitationAction, string> Cases => new()
    {
        // Scheduled: never emailed, so it has no link to accept; resend waits for activation.
        { Scheduled, Accept, "INVITATION_INVALID" },
        { Scheduled, Resend, "COMMUNITY_NOT_ACTIVE" },
        { Scheduled, Revoke, "Revoked" },
        { Scheduled, Activate, "Pending" },
        { Scheduled, Archive, "Revoked" },

        { Pending, Accept, "Accepted" },
        { Pending, Resend, "Pending" },
        { Pending, Revoke, "Revoked" },
        { Pending, Activate, "Pending" },
        { Pending, Archive, "Revoked" },

        // Expired is Pending past ExpiresAt: accept is refused, resend and revoke still work.
        { Expired, Accept, "INVITATION_EXPIRED" },
        { Expired, Resend, "Pending" },
        { Expired, Revoke, "Revoked" },
        { Expired, Activate, "Pending" },
        { Expired, Archive, "Revoked" },

        { Accepted, Accept, "INVITATION_INVALID" },
        { Accepted, Resend, "INVITATION_INVALID" },
        { Accepted, Revoke, "INVITATION_INVALID" },
        { Accepted, Activate, "INVITATION_INVALID" },
        { Accepted, Archive, "INVITATION_INVALID" },

        { Revoked, Accept, "INVITATION_INVALID" },
        { Revoked, Resend, "INVITATION_INVALID" },
        { Revoked, Revoke, "INVITATION_INVALID" },
        { Revoked, Activate, "INVITATION_INVALID" },
        { Revoked, Archive, "INVITATION_INVALID" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryStateAndAction_GivesTheTableOutcome(
        InvitationTransitions.ViewState state, InvitationAction action, string expected)
    {
        var outcome = InvitationTransitions.Apply(state, action);

        if (expected.Contains('_'))
        {
            Assert.Equal(expected, outcome.RefusalCode);
            Assert.Null(outcome.Next);
        }
        else
        {
            Assert.Null(outcome.RefusalCode);
            Assert.Equal(expected, outcome.Next.ToString());
        }
    }

    [Fact]
    public void TheTable_CoversEveryStateAndAction()
    {
        var covered = Cases.Select(c => ((InvitationTransitions.ViewState)c[0], (InvitationAction)c[1])).ToHashSet();
        foreach (var state in Enum.GetValues<InvitationTransitions.ViewState>())
            foreach (var action in Enum.GetValues<InvitationAction>())
                Assert.Contains((state, action), covered);
    }

    [Theory]
    [InlineData(Domain.Enums.InvitationState.Scheduled, false, Scheduled)]
    [InlineData(Domain.Enums.InvitationState.Pending, false, Pending)]
    [InlineData(Domain.Enums.InvitationState.Pending, true, Expired)]
    [InlineData(Domain.Enums.InvitationState.Accepted, true, Accepted)]
    [InlineData(Domain.Enums.InvitationState.Revoked, true, Revoked)]
    public void ViewOf_DerivesExpiredOnlyFromPending(Domain.Enums.InvitationState state, bool pastExpiry,
        InvitationTransitions.ViewState expected)
    {
        var now = new DateTimeOffset(2026, 10, 14, 13, 15, 0, TimeSpan.Zero);
        var invitation = new Domain.Entities.Invitation
        {
            State = state,
            ExpiresAt = state == Domain.Enums.InvitationState.Scheduled ? null : pastExpiry ? now : now.AddSeconds(1),
        };

        Assert.Equal(expected, InvitationTransitions.ViewOf(invitation, now));
    }
}
