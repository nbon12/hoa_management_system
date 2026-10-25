namespace HOAManagementCompany.Features.Company;

// Structured sensitive events for company administration (030 FR-023, FR-024e, FR-039, FR-040;
// research R19). Events identify people, properties and invitations by ID only — never an email
// address, a person's name, a street address or an invitation token. Community profile and
// settings values are not personal data and are logged as old and new values. A change caused by
// another action carries that action's actor and a Cause. A change event written inside a locked
// transaction is held until it commits (PendingEvents); a refusal or a read is written at once.
internal static class CompanyLog
{
    public static void CommunityLifecycle(ILogger logger, string actorId, Guid communityId, string action,
        string? from, string? to, DateTimeOffset at) =>
        PendingEvents.Write(() => logger.LogInformation(
            "CommunityLifecycle: Actor={ActorId} Community={CommunityId} Action={Action} From={From} To={To} At={UtcNow:o}",
            actorId, communityId, action, from, to, at));

    public static void ProfileChanged(ILogger logger, string actorId, Guid communityId, string field,
        string? oldValue, string? newValue, DateTimeOffset at) =>
        PendingEvents.Write(() => logger.LogInformation(
            "CommunityProfileChanged: Actor={ActorId} Community={CommunityId} Field={Field} Old={Old} New={New} At={UtcNow:o}",
            actorId, communityId, field, oldValue, newValue, at));

    public static void DefaultsChanged(ILogger logger, string actorId, Guid companyId, string oldSettings,
        string newSettings, DateTimeOffset at) =>
        PendingEvents.Write(() => logger.LogInformation(
            "CompanyDefaultsChanged: Actor={ActorId} Company={CompanyId} Old={Old} New={New} At={UtcNow:o}",
            actorId, companyId, oldSettings, newSettings, at));

    public static void MembershipChange(ILogger logger, string actorId, Guid communityId, Guid membershipId,
        string targetUserId, string role, string kind, string? cause, DateTimeOffset at) =>
        PendingEvents.Write(() => logger.LogInformation(
            "BoardMembershipChange: Actor={ActorId} Community={CommunityId} Membership={MembershipId} TargetUser={TargetUserId} Role={Role} Kind={Kind} Cause={Cause} At={UtcNow:o}",
            actorId, communityId, membershipId, targetUserId, role, kind, cause, at));

    public static void InvitationChange(ILogger logger, string actorId, Guid? communityId, Guid? propertyId,
        Guid invitationId, string kind, string change, string? reason, string? cause, DateTimeOffset at) =>
        PendingEvents.Write(() => logger.LogInformation(
            "InvitationChange: Actor={ActorId} Community={CommunityId} Property={PropertyId} Invitation={InvitationId} Kind={Kind} Change={Change} Reason={Reason} Cause={Cause} At={UtcNow:o}",
            actorId, communityId, propertyId, invitationId, kind, change, reason, cause, at));

    public static void InvitationAcceptFailed(ILogger logger, Guid? invitationId, Guid? communityId,
        Guid? propertyId, string? userId, string reason, DateTimeOffset at) =>
        logger.LogInformation(
            "InvitationAcceptFailed: Invitation={InvitationId} Community={CommunityId} Property={PropertyId} User={UserId} Reason={Reason} At={UtcNow:o}",
            invitationId?.ToString() ?? "unknown", communityId, propertyId, userId, reason, at);

    public static void ResidentLinkChange(ILogger logger, string actorId, Guid communityId, Guid propertyId,
        string targetUserId, string change, string? cause, DateTimeOffset at) =>
        PendingEvents.Write(() => logger.LogInformation(
            "ResidentLinkChange: Actor={ActorId} Community={CommunityId} Property={PropertyId} TargetUser={TargetUserId} Change={Change} Cause={Cause} At={UtcNow:o}",
            actorId, communityId, propertyId, targetUserId, change, cause, at));

    public static void AutopayCancelled(ILogger logger, string actorId, Guid propertyId, Guid recurringId,
        string reason, string? cause, DateTimeOffset at) =>
        PendingEvents.Write(() => logger.LogInformation(
            "AutopayCancelled: Actor={ActorId} Property={PropertyId} Recurring={RecurringId} Reason={Reason} Cause={Cause} At={UtcNow:o}",
            actorId, propertyId, recurringId, reason, cause, at));

    public static void CompanyAdminChange(ILogger logger, string actorId, Guid companyId, string? targetUserId,
        Guid? invitationId, string change, DateTimeOffset at) =>
        PendingEvents.Write(() => logger.LogInformation(
            "CompanyAdminChange: Actor={ActorId} Company={CompanyId} TargetUser={TargetUserId} Invitation={InvitationId} Change={Change} At={UtcNow:o}",
            actorId, companyId, targetUserId, invitationId, change, at));

    public static void AssociationDataAccess(ILogger logger, string actorId, Guid communityId, string resource,
        DateTimeOffset at) =>
        logger.LogInformation(
            "BoardAssociationDataAccess: Actor={ActorId} Community={CommunityId} Resource={Resource} At={UtcNow:o}",
            actorId, communityId, resource, at);
}
