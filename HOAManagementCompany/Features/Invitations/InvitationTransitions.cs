using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Features.Invitations;

public enum InvitationAction
{
    Accept,
    Resend,
    Revoke,
    Activate,
    Archive
}

/// <summary>
/// The invitation state machine (030 FR-022; data-model.md § Invitation, State transitions), pure
/// so <see cref="InvitationService"/> and its tests share one table. Expired is derived: Pending
/// with now ≥ ExpiresAt. Accepted and Revoked are final.
/// </summary>
public static class InvitationTransitions
{
    public enum ViewState
    {
        Scheduled,
        Pending,
        Expired,
        Accepted,
        Revoked
    }

    /// <summary>The next stored state, or the refusal code (the state is then unchanged).</summary>
    public sealed record Outcome(InvitationState? Next, string? RefusalCode);

    public const string Invalid = "INVITATION_INVALID";
    public const string Expired = "INVITATION_EXPIRED";
    public const string NotActive = "COMMUNITY_NOT_ACTIVE";

    public static ViewState ViewOf(Invitation invitation, DateTimeOffset now) => invitation.State switch
    {
        InvitationState.Scheduled => ViewState.Scheduled,
        InvitationState.Pending => invitation.IsExpired(now) ? ViewState.Expired : ViewState.Pending,
        InvitationState.Accepted => ViewState.Accepted,
        _ => ViewState.Revoked,
    };

    public static Outcome Apply(ViewState state, InvitationAction action) => (state, action) switch
    {
        (ViewState.Accepted or ViewState.Revoked, _) => Refuse(Invalid),

        // A scheduled invitation was never emailed: there is no link to accept, and it is sent
        // only when its community is marked Active (FR-024b).
        (ViewState.Scheduled, InvitationAction.Accept) => Refuse(Invalid),
        (ViewState.Scheduled, InvitationAction.Resend) => Refuse(NotActive),
        (ViewState.Scheduled, InvitationAction.Activate) => To(InvitationState.Pending),

        (ViewState.Pending, InvitationAction.Accept) => To(InvitationState.Accepted),
        (ViewState.Expired, InvitationAction.Accept) => Refuse(Expired),

        // Resend gives a pending or expired invitation a new link and a new 72 hours; activation
        // only moves scheduled ones, so it leaves these as they are.
        (ViewState.Pending or ViewState.Expired, InvitationAction.Resend or InvitationAction.Activate) =>
            To(InvitationState.Pending),

        (_, InvitationAction.Revoke or InvitationAction.Archive) => To(InvitationState.Revoked),

        _ => throw new ArgumentOutOfRangeException(nameof(action), $"{state} × {action} is not in the table."),
    };

    private static Outcome To(InvitationState next) => new(next, null);

    private static Outcome Refuse(string code) => new(null, code);
}
