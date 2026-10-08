using System.Text;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>Configuration for architectural review emails.</summary>
public sealed class ArchitecturalReviewOptions
{
    public const string SectionName = "ArchitecturalReview";

    /// <summary>Absolute frontend origin used for links in emails (e.g. https://app.example.com). Empty → relative links.</summary>
    public string AppBaseUrl { get; set; } = string.Empty;
}

public static class ArcEmailKinds
{
    public const string OwnerApproved = "arc_owner_approved";
    public const string OwnerRevisionsRequested = "arc_owner_revisions_requested";
    public const string OwnerDenied = "arc_owner_denied";
    public const string BoardReminder = "arc_board_reminder";
    public const string BoardLapsed = "arc_board_lapsed";
}

/// <summary>
/// Renders architectural review emails as outbox payloads (027 FR-026/FR-028/FR-028a, contract
/// "Email contract"). Plain text until the designed HTML templates arrive. Owner emails never
/// include vote counts, voter names or board comments.
/// </summary>
public sealed class ArcEmailRenderer(IOptions<ArchitecturalReviewOptions> options)
{
    private string Base => options.Value.AppBaseUrl.TrimEnd('/');

    public string OwnerRequestLink(Guid applicationId) => $"{Base}/app/property/architectural/{applicationId}";
    public string OwnerReviseLink(Guid applicationId) => $"{Base}/app/property/architectural/{applicationId}/revise";
    public string BoardLink(Guid applicationId) => $"{Base}/app/board/architectural?open={applicationId}";

    public static string OwnerKind(ArcOutcome outcome, ArcDenialWording? wording) => outcome switch
    {
        ArcOutcome.Approved => ArcEmailKinds.OwnerApproved,
        _ when wording == ArcDenialWording.Denied => ArcEmailKinds.OwnerDenied,
        _ => ArcEmailKinds.OwnerRevisionsRequested
    };

    public AlertMessage OwnerOutcome(
        ArchitecturalApplication app, string propertyAddress, string ownerFirstName, string ownerEmail,
        string communityName, string formalDisapprovalStatement, DateOnly decisionDate)
    {
        var kind = OwnerKind(app.DecisionOutcome!.Value, app.DecisionWording);
        var subject = kind switch
        {
            ArcEmailKinds.OwnerApproved => $"Your architectural request {app.DisplayId} was approved",
            ArcEmailKinds.OwnerDenied => $"Your architectural request {app.DisplayId} was not approved",
            _ => $"Changes needed: architectural request {app.DisplayId}"
        };

        var b = new StringBuilder();
        b.AppendLine(communityName).AppendLine();
        b.AppendLine($"Hello {ownerFirstName},").AppendLine();
        b.AppendLine(kind switch
        {
            ArcEmailKinds.OwnerApproved => "Your architectural request was approved.",
            ArcEmailKinds.OwnerDenied => "Your architectural request was not approved.",
            _ => "Changes are needed before your architectural request can be approved."
        }).AppendLine();
        Summary(b, app, propertyAddress, decisionDate);

        if (kind == ArcEmailKinds.OwnerApproved)
        {
            if (!string.IsNullOrWhiteSpace(app.ConditionsOfApproval))
                b.AppendLine().AppendLine("Conditions of approval:").AppendLine(app.ConditionsOfApproval);
            b.AppendLine().AppendLine("Please keep the work consistent with what you submitted. Changes need a new request.");
            b.AppendLine().AppendLine($"View your request: {OwnerRequestLink(app.Id)}");
        }
        else
        {
            b.AppendLine().AppendLine(kind == ArcEmailKinds.OwnerDenied ? "Reason from the board:" : "What needs to change:");
            b.AppendLine(app.OwnerReason);
            b.AppendLine().AppendLine(formalDisapprovalStatement);
            b.AppendLine().AppendLine($"Revise and resubmit: {OwnerReviseLink(app.Id)}");
            b.AppendLine($"View your request: {OwnerRequestLink(app.Id)}");
        }

        return new AlertMessage(ownerEmail, subject, b.ToString());
    }

    public AlertMessage BoardReminder(
        ArchitecturalApplication app, string propertyAddress, ArcTallyDto tally, string recipientEmail)
    {
        var b = new StringBuilder();
        b.AppendLine($"A decision on {app.DisplayId} is due {app.DueDate:MM/dd/yy}.").AppendLine();
        Summary(b, app, propertyAddress, null);
        b.AppendLine($"Votes so far: {tally.Approve} approve, {tally.RevisionsNeeded} revisions needed, {tally.Deny} deny, {tally.NotVoted} not voted.");
        b.AppendLine().AppendLine("Questions don't pause the review period. To require changes, vote Revisions needed.");
        b.AppendLine().AppendLine($"Open the application: {BoardLink(app.Id)}");
        return new AlertMessage(recipientEmail, $"Decision due {app.DueDate:MM/dd/yy}: {app.DisplayId}", b.ToString());
    }

    public AlertMessage BoardLapsed(
        ArchitecturalApplication app, string propertyAddress, ArcLapseRule rule, string recipientEmail)
    {
        var label = rule switch
        {
            ArcLapseRule.DeemedApproved => "approved by default",
            ArcLapseRule.DeemedDenied => "denied by default",
            _ => "overdue"
        };
        var b = new StringBuilder();
        b.AppendLine($"The review period for {app.DisplayId} has ended without a board decision. Result: {label}.").AppendLine();
        Summary(b, app, propertyAddress, null);
        b.AppendLine().AppendLine($"Open the application: {BoardLink(app.Id)}");
        return new AlertMessage(recipientEmail, $"Review period ended: {app.DisplayId} ({label})", b.ToString());
    }

    public static OutboxMessage ToOutbox(string kind, AlertMessage message, string dedupKey, Guid? ownerId, string? recipientUserId) =>
        new()
        {
            Kind = kind,
            OwnerId = ownerId,
            RecipientUserId = recipientUserId,
            DedupKey = dedupKey,
            PayloadJson = JsonSerializer.Serialize(message),
            Status = OutboxStatus.Pending
        };

    private static void Summary(StringBuilder b, ArchitecturalApplication app, string propertyAddress, DateOnly? decisionDate)
    {
        b.AppendLine($"Request: {app.DisplayId}{(app.Revision > 1 ? $" v{app.Revision}" : "")}");
        b.AppendLine($"Project: {app.ProjectTitle}");
        b.AppendLine($"Property: {propertyAddress}");
        b.AppendLine($"Submitted: {app.ReceivedDate:MM/dd/yy}");
        if (decisionDate is not null)
            b.AppendLine($"Decision date: {decisionDate:MM/dd/yy}");
    }
}
