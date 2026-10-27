using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Features.Invitations;

/// <summary>
/// What one <see cref="InvitationKind"/> offers (030 R6). <see cref="InvitationService"/> owns the
/// lifecycle shared by every kind — tokens, sending, caps, refusals, sender authority and the
/// description shown in previews and emails — and calls a handler only for what differs: whether
/// the target can be invited, and what accepting gives them.
/// </summary>
public interface IInvitationOfferHandler
{
    InvitationKind Kind { get; }

    /// <summary>
    /// Refuses an invitation its target can't take, before anything is written: for example
    /// <c>ALREADY_MEMBER</c>, <c>ALREADY_RESIDENT</c> or <c>SELF_GRANT_NOT_ALLOWED</c>. Runs inside
    /// the caller's lock.
    /// </summary>
    Task ValidateCreateAsync(Invitation invitation, CancellationToken ct);

    /// <summary>
    /// Gives <paramref name="userId"/> what the invitation offers, inside the caller's lock and
    /// transaction. Returns true when they already held it, so nothing was added (FR-022).
    /// </summary>
    Task<bool> ApplyAsync(Invitation invitation, string userId, CancellationToken ct);
}

/// <summary>
/// What an invitation offers, as the preview and the email show it. <see cref="CommunityName"/> is
/// null for a Company Administrator invitation; <see cref="SentBy"/> is the sender's display name,
/// or null for the setup step.
/// </summary>
public sealed record InvitationDescription(
    InvitationKind Kind,
    string CompanyName,
    string? CommunityName,
    CommunityRole? Role,
    DateOnly? TermEndDate,
    string? PropertyAddress,
    string? SentBy,
    string TimeZoneId);
