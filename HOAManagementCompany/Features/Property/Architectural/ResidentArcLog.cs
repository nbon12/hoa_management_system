namespace HOAManagementCompany.Features.Property.Architectural;

// Structured sensitive events for resident architectural requests (029, constitution §7,
// 025 FR-017). Properties carry IDs and codes only — never file names, file bytes, owner names,
// storage keys, descriptions or reply text.
internal static class ResidentArcLog
{
    public static void AccessDenied(ILogger logger, string actorId, Guid propertyId, string resource) =>
        logger.LogWarning(
            "ArcResidentAccessDenied: Actor={ActorId} Property={PropertyId} Resource={Resource}",
            actorId, propertyId, resource);

    public static void AttachmentAccess(ILogger logger, string actorId, Guid propertyId, string resource, DateTimeOffset at) =>
        logger.LogInformation(
            "ArcResidentAttachmentAccess: Actor={ActorId} Property={PropertyId} Resource={Resource} At={UtcNow:o}",
            actorId, propertyId, resource, at);

    public static void UploadRejected(ILogger logger, string actorId, Guid propertyId, string reason) =>
        logger.LogWarning(
            "ArcUploadRejected: Actor={ActorId} Property={PropertyId} Reason={Reason}",
            actorId, propertyId, reason);

    public static void Submitted(ILogger logger, string actorId, Guid communityId, Guid applicationId, int revision, DateTimeOffset at) =>
        logger.LogInformation(
            "ArcSubmitted: Actor={ActorId} Community={CommunityId} Application={ApplicationId} Revision={Revision} At={UtcNow:o}",
            actorId, communityId, applicationId, revision, at);

    public static void Withdrawn(ILogger logger, string actorId, Guid communityId, Guid applicationId, DateTimeOffset at) =>
        logger.LogInformation(
            "ArcWithdrawn: Actor={ActorId} Community={CommunityId} Application={ApplicationId} At={UtcNow:o}",
            actorId, communityId, applicationId, at);

    public static void InfoReplied(ILogger logger, string actorId, Guid applicationId, Guid infoRequestId, DateTimeOffset at) =>
        logger.LogInformation(
            "ArcInfoReplied: Actor={ActorId} Application={ApplicationId} InfoRequest={InfoRequestId} At={UtcNow:o}",
            actorId, applicationId, infoRequestId, at);

    public static void RevisionDraftCreated(ILogger logger, string actorId, Guid previousRevisionId, Guid draftId, DateTimeOffset at) =>
        logger.LogInformation(
            "ArcRevisionDraftCreated: Actor={ActorId} PreviousRevision={PreviousRevisionId} Draft={DraftId} At={UtcNow:o}",
            actorId, previousRevisionId, draftId, at);
}
