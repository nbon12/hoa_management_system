using System.Text;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Payments.Alerts;

namespace HOAManagementCompany.Features.Company;

/// <summary>The non-invitation emails 030 sends through the outbox (contracts/emails.md; R10). Each ≤ 30 characters.</summary>
public static class CompanyEmailKinds
{
    public const string CommunityAdded = "co_community_added";
    public const string CommunityArchived = "co_community_archived";
    public const string ResidentRemoved = "co_resident_removed";
    public const string AutopayEnded = "co_autopay_ended";
    public const string AdminRecovery = "co_admin_recovery";

    public static readonly string[] All = [CommunityAdded, CommunityArchived, ResidentRemoved, AutopayEnded, AdminRecovery];
}

/// <summary>
/// Renders 030's outbox emails as plain text (the HTML layouts come from Claude Design brief 12 and
/// replace these bodies without changing the fields). Every recipient has an account, so rows
/// target <see cref="OutboxMessage.RecipientUserId"/>. No body ever contains an invitation link.
/// </summary>
public sealed class CompanyEmailRenderer(AppLinks links)
{
    public sealed record PropertyLine(string Address, bool AutopayEnded);

    public OutboxMessage CommunityAdded(string recipientUserId, string recipientEmail, Guid communityId,
        string communityName, string managerName) =>
        Row(CompanyEmailKinds.CommunityAdded, recipientUserId, $"co:community-added:{communityId}:{recipientUserId}",
            new AlertMessage(recipientEmail, $"{communityName} was added and is waiting to be marked Active",
                $"{managerName} added {communityName}. It is in Onboarding and hidden from residents until a Company Administrator marks it Active.\n\n"
                + $"Open its Community settings: {links.Link($"/app/company/communities/{communityId}/settings/profile")}\n"));

    public OutboxMessage CommunityArchived(string recipientUserId, string recipientEmail, Guid communityId,
        string communityName, string companyName, DateTimeOffset archivedAt, IReadOnlyList<PropertyLine> properties)
    {
        var body = new StringBuilder();
        body.Append($"{companyName} no longer manages {communityName}.\n\n");
        body.Append(properties.Count == 1 ? "Your home: " : "Your homes:\n");
        body.Append(properties.Count == 1
            ? $"{properties[0].Address}\n"
            : string.Concat(properties.Select(p => $"- {p.Address}\n")));
        body.Append("\nYour payment history stays readable.\n");
        foreach (var ended in properties.Where(p => p.AutopayEnded))
            body.Append($"Autopay for {ended.Address} has ended.\n");

        return Row(CompanyEmailKinds.CommunityArchived, recipientUserId,
            $"co:archived:{communityId}:{archivedAt:yyyyMMddHHmmss}:{recipientUserId}",
            new AlertMessage(recipientEmail, $"{companyName} no longer manages {communityName}", body.ToString()));
    }

    public OutboxMessage ResidentRemoved(string recipientUserId, string recipientEmail, Guid propertyId,
        string propertyAddress, string communityName, bool autopayEnded, DateTimeOffset removedAt) =>
        Row(CompanyEmailKinds.ResidentRemoved, recipientUserId,
            $"co:removed:{propertyId}:{recipientUserId}:{removedAt:yyyyMMddHHmmss}",
            new AlertMessage(recipientEmail, $"You were removed from {propertyAddress}",
                $"You are no longer a resident of {propertyAddress} in {communityName}.\n"
                + (autopayEnded ? "Autopay you set up for this home has ended.\n" : "")
                + "\nContact your community manager if this is a mistake.\n"));

    public OutboxMessage AutopayEnded(string recipientUserId, string recipientEmail, Guid recurringId,
        string propertyAddress, DateTimeOffset at) =>
        Row(CompanyEmailKinds.AutopayEnded, recipientUserId,
            $"co:autopay-ended:{recurringId}:{recipientUserId}:{at:yyyyMMddHHmmss}",
            new AlertMessage(recipientEmail, $"Autopay for {propertyAddress} has ended",
                $"Autopay for {propertyAddress} has ended. You can set it up again from Payments if you want it.\n"
                + $"{links.Link("/app/payments/recurring")}\n"));

    public OutboxMessage AdminRecovery(string recipientUserId, string recipientEmail, Guid grantOrInvitationId,
        string grantedNameOrEmail, DateTimeOffset at) =>
        Row(CompanyEmailKinds.AdminRecovery, recipientUserId, $"co:recovery:{grantOrInvitationId}:{recipientUserId}",
            new AlertMessage(recipientEmail, "A Company Administrator was added in recovery mode",
                $"{grantedNameOrEmail} was made a Company Administrator (or invited to be one) at {at.UtcDateTime:yyyy-MM-dd HH:mm} UTC.\n\n"
                + "This was done from the command line by your deployment's operator.\n"));

    private static OutboxMessage Row(string kind, string recipientUserId, string dedupKey, AlertMessage message) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        RecipientUserId = recipientUserId,
        DedupKey = dedupKey,
        PayloadJson = JsonSerializer.Serialize(message),
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
