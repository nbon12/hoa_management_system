using HOAManagementCompany.Features.Auth;

namespace HOAManagementCompany.Features.Company;

/// <summary>
/// The error codes 030 adds or reuses, each with the HTTP status from
/// <c>contracts/invitations-and-auth.md § Error codes</c> and the plain-language copy from design
/// conventions §6 (FR-042). Thrown as <see cref="DomainException"/> and written by the global
/// handler as <c>{ code, message, …extensions }</c>.
/// </summary>
public static class CompanyErrors
{
    public sealed record FieldError(string Field, string Message);

    public sealed record OpenWorkItem(string Kind, string Label, string Status);

    public static DomainException Forbidden() =>
        new("FORBIDDEN", "You do not have access to this community.", StatusCodes.Status403Forbidden);

    /// <summary>FR-007: names the other community only for a Company Administrator.</summary>
    public static DomainException CommunityNameTaken(string? otherCommunityName, bool otherIsArchived) =>
        new("COMMUNITY_NAME_TAKEN",
            otherCommunityName is null
                ? "That community name is unavailable."
                : otherIsArchived
                    ? $"{otherCommunityName} is the name of an archived community. Archived communities keep their names. Choose a different community name."
                    : $"{otherCommunityName} is already the name of another community. Choose a different community name.",
            StatusCodes.Status409Conflict);

    public static DomainException NoActiveManager(string communityName) =>
        new("NO_ACTIVE_MANAGER",
            $"{communityName} can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted.",
            StatusCodes.Status409Conflict);

    public static DomainException OpenWork(string communityName, IReadOnlyList<OpenWorkItem> items) =>
        new("OPEN_WORK",
            $"{communityName} can't be archived until this work is closed: "
                + string.Join("; ", items.Select(i => $"{i.Label} · {i.Status}")) + ".",
            StatusCodes.Status409Conflict,
            new Dictionary<string, object?> { ["items"] = items });

    public static DomainException InvalidTransition(string message) =>
        new("INVALID_TRANSITION", message, StatusCodes.Status409Conflict);

    public static DomainException CommunityArchived(string communityName) =>
        new("COMMUNITY_ARCHIVED",
            $"{communityName} is archived, so it can't be changed. Restore it to make changes.",
            StatusCodes.Status409Conflict);

    public static DomainException CommunityNotActive(string message = "This home's community isn't live yet.") =>
        new("COMMUNITY_NOT_ACTIVE", message, StatusCodes.Status409Conflict);

    public static DomainException ParentArchived(string parentName, string? childName = null) =>
        new("PARENT_ARCHIVED",
            childName is null
                ? $"{parentName} is archived, so it can't be a parent."
                : $"{childName} can't be restored while its parent, {parentName}, is archived. Restore {parentName} first.",
            StatusCodes.Status409Conflict);

    public static DomainException ParentCycle(string childName, string parentName) =>
        new("PARENT_CYCLE",
            $"{parentName} can't be the parent of {childName}, because {parentName} is already under {childName}. Choose a different parent.",
            StatusCodes.Status422UnprocessableEntity);

    public static DomainException InvitationInvalid() =>
        new("INVITATION_INVALID",
            "This invitation link can't be used. It may have been cancelled, already used, or replaced by a newer link. If you still need access, ask the person who invited you.",
            StatusCodes.Status410Gone);

    public static DomainException InvitationExpired() =>
        new("INVITATION_EXPIRED",
            "This invitation has expired. Invitation links work for 72 hours. Ask the person who invited you to send a new link.",
            StatusCodes.Status410Gone);

    /// <summary>FR-022: never includes the invited email.</summary>
    public static DomainException InvitationEmailMismatch() =>
        new("INVITATION_EMAIL_MISMATCH",
            "This invitation is for a different email address. Sign out, then open the link again and sign in or create an account with the email address the invitation was sent to.",
            StatusCodes.Status403Forbidden);

    public static DomainException InvitationPending(Guid invitationId, string message) =>
        new("INVITATION_PENDING", message, StatusCodes.Status409Conflict,
            new Dictionary<string, object?> { ["invitationId"] = invitationId });

    public static DomainException AlreadyMember(string message) =>
        new("ALREADY_MEMBER", message, StatusCodes.Status409Conflict);

    public static DomainException AlreadyResident(string message) =>
        new("ALREADY_RESIDENT", message, StatusCodes.Status409Conflict);

    public static DomainException SelfGrantNotAllowed(bool companyAdministratorPage = false) =>
        new("SELF_GRANT_NOT_ALLOWED",
            companyAdministratorPage
                ? "You can't invite yourself. Ask another Company Administrator."
                : "You can't give yourself a role or a home. Ask another Company Administrator or this community's Community Manager.",
            StatusCodes.Status422UnprocessableEntity);

    public static DomainException LastManager(string message) =>
        new("LAST_MANAGER", message, StatusCodes.Status422UnprocessableEntity);

    public static DomainException LastCompanyAdmin(string message =
        "You're the only Company Administrator, so your role can't be revoked. Invite another Company Administrator first. Once they accept, your role can be revoked.") =>
        new("LAST_COMPANY_ADMIN", message, StatusCodes.Status422UnprocessableEntity);

    public static DomainException PropertyAccessDenied() =>
        new("PROPERTY_ACCESS_DENIED", "You no longer have access to that home.", StatusCodes.Status403Forbidden);

    public static DomainException RegistrationFailed() =>
        new("REGISTRATION_FAILED",
            "We couldn't create an account with these details. If you already have an account, sign in to accept the invitation.",
            StatusCodes.Status422UnprocessableEntity);

    /// <summary>FR-041c: one message for every limit, so a cap can't hint that someone else invited an email.</summary>
    public static DomainException RateLimited() =>
        new("RATE_LIMITED", "Too many requests. Wait a few minutes, then try again.", StatusCodes.Status429TooManyRequests);

    public static DomainException NotFound(string what) =>
        new("NOT_FOUND", $"{what} was not found.", StatusCodes.Status404NotFound);

    public static DomainException Validation(params FieldError[] errors) =>
        new("VALIDATION_ERROR",
            errors.Length == 1 ? errors[0].Message : "One or more validation errors occurred.",
            StatusCodes.Status422UnprocessableEntity,
            new Dictionary<string, object?>
            {
                ["errors"] = errors.Select(e => new { field = e.Field, message = e.Message }).ToList()
            });
}
