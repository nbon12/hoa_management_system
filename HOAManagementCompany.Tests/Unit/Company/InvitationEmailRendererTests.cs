using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Invitations;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Company;

/// <summary>
/// 030 T049 — the three direct-send invitation emails (contracts/emails.md; design brief 12 §5.2,
/// plain-text form §5.4): subjects, the body fields, the expiry in local time, and the setup-step
/// sender line.
/// </summary>
public class InvitationEmailRendererTests
{
    private const string Link = "https://app.example.com/invite#t=abc";
    private static readonly DateTimeOffset Expires = new(2026, 10, 13, 20, 40, 0, TimeSpan.Zero); // 4:40 PM EDT

    private static InvitationDescription Resident(string? sentBy = "Jordan Blake") => new(
        InvitationKind.Residency, "Juniper Row Community Management", "Sakura Heights", null, null,
        "711 Keystone Park Dr #29", sentBy, "America/New_York");

    [Fact]
    public void ResidentInvitation_MatchesTheBriefsPlainTextForm()
    {
        var msg = InvitationEmailRenderer.Render(Resident(), "kim@example.com", Link, Expires, hasAccount: false, resent: false);

        Assert.Equal("kim@example.com", msg.Target);
        Assert.Equal("You're invited to be a resident of 711 Keystone Park Dr #29", msg.Subject);
        Assert.Equal("""
            Sakura Heights

            Hello,

            Jordan Blake invited you to be a resident of 711 Keystone Park Dr #29 in Sakura Heights.

            You're invited as: Resident of this home
            Home: 711 Keystone Park Dr #29
            Community: Sakura Heights
            Sent by: Jordan Blake
            For: kim@example.com
            Expires: Oct 13, 2026, 4:40 PM EDT

            After you accept, you'll see this home when you sign in to NekoHOA.

            To accept, create an account with your name and a password. You don't need a claim code. This link confirms your email address.

            The link works once, and only for kim@example.com. It expires Oct 13, 2026, 4:40 PM EDT, 72 hours after it was sent.

            Accept invitation: https://app.example.com/invite#t=abc

            If you weren't expecting this, you can ignore this email. Nothing happens unless you accept.

            Sent by NekoHOA for Juniper Row Community Management.
            You're getting this because Jordan Blake invited kim@example.com.

            """.ReplaceLineEndings("\n"), msg.Body);
    }

    [Fact]
    public void ResidentInvitation_WithAnAccount_AsksToSignIn_AndResentSaysTheOldLinkIsDead()
    {
        var msg = InvitationEmailRenderer.Render(Resident(), "sam@example.com", Link, Expires, hasAccount: true, resent: true);

        Assert.Contains("You already have a NekoHOA account for sam@example.com. Sign in with it, then accept. Nothing changes until you accept. Your other homes don't change.", msg.Body);
        Assert.DoesNotContain("create an account", msg.Body);
        Assert.Contains("This invitation replaces an earlier one. The link in the earlier email no longer works.", msg.Body);
        Assert.Contains($"Sign in to accept: {Link}", msg.Body);
    }

    [Theory]
    [InlineData(CommunityRole.CommunityManager, null, "You're invited to be a Community Manager of Keystone Park",
        "Robin Hale invited you to be a Community Manager of Keystone Park.",
        "You're invited as: Community Manager", "The role starts when you accept. It has no end date.")]
    [InlineData(CommunityRole.BoardMember, "2027-12-31", "You're invited to be a Board Member of Keystone Park",
        "Robin Hale invited you to be a Board Member of Keystone Park.",
        "You're invited as: Board Member, until Dec 31, 2027", "The role starts when you accept and ends Dec 31, 2027.")]
    [InlineData(CommunityRole.Accountant, null, "You're invited to be an Accountant of Keystone Park",
        "Robin Hale invited you to be an Accountant of Keystone Park.",
        "You're invited as: Accountant", "The role starts when you accept. It has no end date.")]
    public void RoleInvitation_NamesTheRoleAndTerm(
        CommunityRole role, string? termEnd, string subject, string lead, string asLine, string termLine)
    {
        var description = new InvitationDescription(InvitationKind.Membership, "Juniper Row Community Management",
            "Keystone Park", role, termEnd is null ? null : DateOnly.Parse(termEnd), null, "Robin Hale", "America/New_York");

        var msg = InvitationEmailRenderer.Render(description, "pat@example.com", Link, Expires, hasAccount: false, resent: false);

        Assert.Equal(subject, msg.Subject);
        Assert.StartsWith("Keystone Park\n", msg.Body);
        Assert.Contains(lead + "\n", msg.Body);
        Assert.Contains(asLine + "\n", msg.Body);
        Assert.Contains("Community: Keystone Park\nSent by: Robin Hale\nFor: pat@example.com\nExpires: Oct 13, 2026, 4:40 PM EDT\n", msg.Body);
        Assert.Contains(termLine, msg.Body);
        Assert.Contains($"Accept invitation: {Link}", msg.Body);
    }

    [Fact]
    public void CompanyAdministratorInvitation_UsesTheCompany()
    {
        var description = new InvitationDescription(InvitationKind.CompanyAdministrator, "Juniper Row Community Management",
            null, null, null, null, "Robin Hale", "America/New_York");

        var msg = InvitationEmailRenderer.Render(description, "ops@example.com", Link, Expires, hasAccount: false, resent: false);

        Assert.Equal("You're invited to be a Company Administrator of Juniper Row Community Management", msg.Subject);
        Assert.StartsWith("Juniper Row Community Management\n", msg.Body);
        Assert.Contains("Robin Hale invited you to be a Company Administrator of Juniper Row Community Management.", msg.Body);
        Assert.Contains("Company: Juniper Row Community Management\n", msg.Body);
        Assert.Contains("After you accept, you can open the company area for Juniper Row Community Management when you sign in.", msg.Body);
        Assert.Contains("You're getting this because Robin Hale invited ops@example.com.", msg.Body);
    }

    [Fact]
    public void SetupStepInvitation_SaysTheSetupStepSentIt()
    {
        var description = new InvitationDescription(InvitationKind.CompanyAdministrator, "Juniper Row Community Management",
            null, null, null, null, null, "America/New_York");

        var msg = InvitationEmailRenderer.Render(description, "backup@example.com", Link, Expires, hasAccount: false, resent: false);

        Assert.Contains("You're invited to be a Company Administrator of Juniper Row Community Management.\n\nThe setup step sent this invitation. Whoever deploys NekoHOA runs that step.", msg.Body);
        Assert.Contains("Sent by: Setup step\n", msg.Body);
        Assert.Contains("You're getting this because the setup step invited backup@example.com.", msg.Body);
    }

    [Theory]
    [InlineData("America/New_York", "Oct 13, 2026, 4:40 PM EDT")]
    [InlineData("America/Chicago", "Oct 13, 2026, 3:40 PM CDT")]
    [InlineData("America/Los_Angeles", "Oct 13, 2026, 1:40 PM PDT")]
    [InlineData("America/Phoenix", "Oct 13, 2026, 1:40 PM MST")]
    [InlineData("Pacific/Honolulu", "Oct 13, 2026, 10:40 AM HST")]
    [InlineData("Europe/Berlin", "Oct 13, 2026, 10:40 PM UTC+02:00")]
    [InlineData("UTC", "Oct 13, 2026, 8:40 PM UTC")]
    public void LocalTime_IsShownInTheGivenZone(string timeZoneId, string expected)
    {
        Assert.Equal(expected, LocalTimeText.Format(Expires, timeZoneId));
    }

    [Fact]
    public void LocalTime_InWinter_UsesStandardTime()
    {
        Assert.Equal("Jan 5, 2027, 9:15 AM EST", LocalTimeText.Format(new DateTimeOffset(2027, 1, 5, 14, 15, 0, TimeSpan.Zero), "America/New_York"));
    }
}
