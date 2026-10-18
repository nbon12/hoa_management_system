using System.Globalization;
using System.Text;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Infrastructure.Payments.Alerts;

namespace HOAManagementCompany.Features.Invitations;

/// <summary>
/// The three invitation emails, sent directly rather than through the outbox because they carry a
/// live link (030 R6; contracts/emails.md). Plain text in design brief 12's §5.4 form, with its
/// §5.2 copy; the HTML layouts replace the body later without changing the fields. Times are in
/// the invitation's time zone. Bodies hold the link, so they are never logged.
/// </summary>
public static class InvitationEmailRenderer
{
    private const string Ignore = "If you weren't expecting this, you can ignore this email. Nothing happens unless you accept.";

    public static AlertMessage Render(InvitationDescription invitation, string email, string link,
        DateTimeOffset expiresAt, bool hasAccount, bool resent)
    {
        var expires = LocalTimeText.Format(expiresAt, invitation.TimeZoneId);
        var sender = invitation.SentBy;
        var company = invitation.CompanyName;

        var (context, subject, lead, asLine, placeRows, gives) = invitation.Kind switch
        {
            InvitationKind.Membership => (
                invitation.CommunityName!,
                $"You're invited to be {RoleWithArticle(invitation.Role)} of {invitation.CommunityName}",
                $"{sender} invited you to be {RoleWithArticle(invitation.Role)} of {invitation.CommunityName}.",
                invitation.TermEndDate is { } end ? $"{RoleLabel(invitation.Role)}, until {Day(end)}" : RoleLabel(invitation.Role),
                $"Community: {invitation.CommunityName}",
                invitation.TermEndDate is { } until
                    ? $"The role starts when you accept and ends {Day(until)}."
                    : "The role starts when you accept. It has no end date."),
            InvitationKind.Residency => (
                invitation.CommunityName!,
                $"You're invited to be a resident of {invitation.PropertyAddress}",
                $"{sender} invited you to be a resident of {invitation.PropertyAddress} in {invitation.CommunityName}.",
                "Resident of this home",
                $"Home: {invitation.PropertyAddress}\nCommunity: {invitation.CommunityName}",
                "After you accept, you'll see this home when you sign in to NekoHOA."),
            _ => (
                company,
                $"You're invited to be a Company Administrator of {company}",
                sender is null
                    ? $"You're invited to be a Company Administrator of {company}.\n\nThe setup step sent this invitation. Whoever deploys NekoHOA runs that step."
                    : $"{sender} invited you to be a Company Administrator of {company}.",
                "Company Administrator",
                $"Company: {company}",
                $"After you accept, you can open the company area for {company} when you sign in."),
        };

        var howToAccept = hasAccount
            ? $"You already have a NekoHOA account for {email}. Sign in with it, then accept. Nothing changes until you accept."
              + (invitation.Kind == InvitationKind.Residency ? " Your other homes don't change." : "")
            : "To accept, create an account with your name and a password. You don't need a claim code. This link confirms your email address.";

        var body = new StringBuilder()
            .Append($"{context}\n\n")
            .Append("Hello,\n\n")
            .Append($"{lead}\n\n")
            .Append($"You're invited as: {asLine}\n")
            .Append($"{placeRows}\n")
            .Append($"Sent by: {sender ?? "Setup step"}\n")
            .Append($"For: {email}\n")
            .Append($"Expires: {expires}\n\n")
            .Append($"{gives}\n\n")
            .Append($"{howToAccept}\n\n")
            .Append($"The link works once, and only for {email}. It expires {expires}, 72 hours after it was sent.\n\n");
        if (resent)
            body.Append("This invitation replaces an earlier one. The link in the earlier email no longer works.\n\n");
        body.Append($"{(hasAccount ? "Sign in to accept" : "Accept invitation")}: {link}\n\n")
            .Append($"{Ignore}\n\n")
            .Append($"Sent by NekoHOA for {company}.\n")
            .Append(sender is null
                ? $"You're getting this because the setup step invited {email}.\n"
                : $"You're getting this because {sender} invited {email}.\n");

        return new AlertMessage(email, subject, body.ToString());
    }

    public static string RoleLabel(CommunityRole? role) => role switch
    {
        CommunityRole.CommunityManager => "Community Manager",
        CommunityRole.BoardMember => "Board Member",
        CommunityRole.Accountant => "Accountant",
        _ => role?.ToString() ?? string.Empty,
    };

    private static string RoleWithArticle(CommunityRole? role) =>
        role == CommunityRole.Accountant ? $"an {RoleLabel(role)}" : $"a {RoleLabel(role)}";

    private static string Day(DateOnly date) => date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}

/// <summary>
/// A time as design brief 12 shows it — "Oct 14, 2026, 9:15 AM EDT" — in a community's or the
/// company default's time zone (030 FR-022). .NET has no zone abbreviations, so the US zones
/// carry theirs and any other zone shows its UTC offset.
/// </summary>
public static class LocalTimeText
{
    private static readonly Dictionary<string, (string Standard, string Daylight)> Abbreviations = new(StringComparer.Ordinal)
    {
        ["America/New_York"] = ("EST", "EDT"),
        ["America/Detroit"] = ("EST", "EDT"),
        ["America/Indiana/Indianapolis"] = ("EST", "EDT"),
        ["America/Kentucky/Louisville"] = ("EST", "EDT"),
        ["America/Chicago"] = ("CST", "CDT"),
        ["America/Denver"] = ("MST", "MDT"),
        ["America/Boise"] = ("MST", "MDT"),
        ["America/Phoenix"] = ("MST", "MST"),
        ["America/Los_Angeles"] = ("PST", "PDT"),
        ["America/Anchorage"] = ("AKST", "AKDT"),
        ["Pacific/Honolulu"] = ("HST", "HST"),
        ["UTC"] = ("UTC", "UTC"),
        ["Etc/UTC"] = ("UTC", "UTC"),
    };

    public static string Format(DateTimeOffset at, string timeZoneId)
    {
        var zone = ArcQueries.ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(at, zone);
        var text = local.ToString("MMM d, yyyy, h:mm tt", CultureInfo.InvariantCulture);
        var abbreviation = Abbreviations.TryGetValue(timeZoneId, out var names)
            ? zone.IsDaylightSavingTime(local) ? names.Daylight : names.Standard
            : $"UTC{(local.Offset < TimeSpan.Zero ? "-" : "+")}{local.Offset:hh\\:mm}";
        return $"{text} {abbreviation}";
    }
}
