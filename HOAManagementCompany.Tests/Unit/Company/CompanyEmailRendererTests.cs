using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Company;

/// <summary>
/// 030 T033 — the co_* outbox emails (contracts/emails.md): kinds fit the 30-character column,
/// dedup keys follow the contract, and bodies carry the listed fields and never an invitation link.
/// </summary>
public class CompanyEmailRendererTests
{
    private static readonly CompanyEmailRenderer Renderer =
        new(new AppLinks(Options.Create(new AppLinksOptions { BaseUrl = "https://app.example.com" })));
    private static readonly DateTimeOffset At = new(2026, 10, 12, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid CommunityId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void EveryKind_FitsTheOutboxKindColumn()
    {
        Assert.All(CompanyEmailKinds.All, kind => Assert.InRange(kind.Length, 1, 30));
    }

    [Fact]
    public void CommunityAdded_NamesTheManager_AndLinksToTheSettingsPage()
    {
        var row = Renderer.CommunityAdded("u1", "robin@example.com", CommunityId, "Keystone Ridge", "Jordan Blake");
        var msg = Payload(row);

        Assert.Equal(CompanyEmailKinds.CommunityAdded, row.Kind);
        Assert.Equal("u1", row.RecipientUserId);
        Assert.Null(row.OwnerId);
        Assert.Equal($"co:community-added:{CommunityId}:u1", row.DedupKey);
        Assert.Equal("robin@example.com", msg.Target);
        Assert.Equal("Keystone Ridge was added and is waiting to be marked Active", msg.Subject);
        Assert.Contains("Jordan Blake", msg.Body);
        Assert.Contains($"https://app.example.com/app/company/communities/{CommunityId}/settings/profile", msg.Body);
    }

    [Fact]
    public void CommunityArchived_ListsEachHome_AndSaysWhereAutopayEnded()
    {
        var row = Renderer.CommunityArchived("u2", "jane@example.com", CommunityId, "Old Mill Commons",
            "Juniper Row Community Management", At,
            [new("16 Old Mill Ln", true), new("18 Old Mill Ln", false)]);
        var msg = Payload(row);

        Assert.Equal($"co:archived:{CommunityId}:20261012140000:u2", row.DedupKey);
        Assert.Equal("Juniper Row Community Management no longer manages Old Mill Commons", msg.Subject);
        Assert.Contains("16 Old Mill Ln", msg.Body);
        Assert.Contains("18 Old Mill Ln", msg.Body);
        Assert.Contains("Your payment history stays readable.", msg.Body);
        Assert.Contains("Autopay for 16 Old Mill Ln has ended.", msg.Body);
        Assert.DoesNotContain("Autopay for 18 Old Mill Ln", msg.Body);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResidentRemoved_MentionsAutopayOnlyWhenItEnded(bool autopayEnded)
    {
        var propertyId = Guid.NewGuid();
        var row = Renderer.ResidentRemoved("u3", "jane@example.com", propertyId, "711 Keystone Park Dr #29",
            "Sakura Heights", autopayEnded, At);
        var msg = Payload(row);

        Assert.Equal($"co:removed:{propertyId}:u3:20261012140000", row.DedupKey);
        Assert.Equal("You were removed from 711 Keystone Park Dr #29", msg.Subject);
        Assert.Contains("Sakura Heights", msg.Body);
        Assert.Contains("Contact your community manager if this is a mistake.", msg.Body);
        Assert.Equal(autopayEnded, msg.Body.Contains("Autopay you set up for this home has ended."));
    }

    [Fact]
    public void AutopayEnded_NeverNamesTheRemovedResident()
    {
        var recurringId = Guid.NewGuid();
        var row = Renderer.AutopayEnded("u4", "sam@example.com", recurringId, "711 Keystone Park Dr #29", At);
        var msg = Payload(row);

        Assert.Equal($"co:autopay-ended:{recurringId}:u4:20261012140000", row.DedupKey);
        Assert.Equal("Autopay for 711 Keystone Park Dr #29 has ended", msg.Subject);
        Assert.Contains("set it up again from Payments", msg.Body);
    }

    [Fact]
    public void AdminRecovery_SaysItWasTheOperator()
    {
        var grantId = Guid.NewGuid();
        var row = Renderer.AdminRecovery("u5", "robin@example.com", grantId, "owner@example.com", At);
        var msg = Payload(row);

        Assert.Equal($"co:recovery:{grantId}:u5", row.DedupKey);
        Assert.Equal("A Company Administrator was added in recovery mode", msg.Subject);
        Assert.Contains("2026-10-12 14:00 UTC", msg.Body);
        Assert.Contains("from the command line by your deployment's operator", msg.Body);
    }

    [Fact]
    public void NoBody_ContainsAnInvitationLink()
    {
        var bodies = new[]
        {
            Renderer.CommunityAdded("u", "a@example.com", CommunityId, "C", "M"),
            Renderer.CommunityArchived("u", "a@example.com", CommunityId, "C", "Co", At, [new("A", true)]),
            Renderer.ResidentRemoved("u", "a@example.com", Guid.NewGuid(), "A", "C", true, At),
            Renderer.AutopayEnded("u", "a@example.com", Guid.NewGuid(), "A", At),
            Renderer.AdminRecovery("u", "a@example.com", Guid.NewGuid(), "x", At),
        }.Select(r => Payload(r).Body);

        Assert.All(bodies, b => Assert.DoesNotContain("/invite", b));
    }

    private static AlertMessage Payload(OutboxMessage row) => JsonSerializer.Deserialize<AlertMessage>(row.PayloadJson)!;
}
