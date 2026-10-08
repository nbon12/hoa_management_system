namespace HOAManagementCompany.Features.Board.Architectural;

// Structured sensitive events for architectural review (027 constitution §7, 025 FR-017).
// Properties carry IDs and enum values only — never comment text, owner names or storage keys
// (027 plan, Observability; tasks T080).
internal static class ArcLog
{
    public static void SensitiveAccess(ILogger logger, string actorId, Guid communityId, string resource, DateTimeOffset at) =>
        logger.LogInformation(
            "ArcSensitiveAccess: Actor={ActorId} Community={CommunityId} Resource={Resource} At={UtcNow:o}",
            actorId, communityId, resource, at);

    public static void VoteCast(ILogger logger, string actorId, Guid communityId, Guid applicationId, string choice, DateTimeOffset at) =>
        logger.LogInformation(
            "ArcVoteCast: Actor={ActorId} Community={CommunityId} Application={ApplicationId} Choice={Choice} At={UtcNow:o}",
            actorId, communityId, applicationId, choice, at);

    public static void OutcomeRecorded(ILogger logger, string actorId, Guid communityId, Guid applicationId, string outcome, string? wording, DateTimeOffset at) =>
        logger.LogInformation(
            "ArcOutcomeRecorded: Actor={ActorId} Community={CommunityId} Application={ApplicationId} Outcome={Outcome} Wording={Wording} At={UtcNow:o}",
            actorId, communityId, applicationId, outcome, wording, at);

    public static void SettingsChanged(ILogger logger, string actorId, Guid communityId, string oldSettings, string newSettings, DateTimeOffset at) =>
        logger.LogInformation(
            "ArcSettingsChanged: Actor={ActorId} Community={CommunityId} Old={OldSettings} New={NewSettings} At={UtcNow:o}",
            actorId, communityId, oldSettings, newSettings, at);

    public static void LapseApplied(ILogger logger, Guid communityId, Guid applicationId, string rule, DateTimeOffset at) =>
        logger.LogInformation(
            "ArcLapseApplied: Community={CommunityId} Application={ApplicationId} Rule={Rule} At={UtcNow:o}",
            communityId, applicationId, rule, at);
}
