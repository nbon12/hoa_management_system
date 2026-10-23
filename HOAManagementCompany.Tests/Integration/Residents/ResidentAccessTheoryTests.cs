using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Residents;

/// <summary>
/// 030 T035 — every resident endpoint re-checks, on each request and from stored data, that the
/// caller is still linked to the selected home and that the home's community status allows the
/// operation (FR-024g, FR-012a; contracts/invitations-and-auth.md § Resident access). One row per
/// endpoint; each row plays a linked resident against an Active, an Onboarding and an Archived
/// home, a token for a home the caller isn't linked to, and a token with no home.
/// </summary>
public class ResidentAccessTheoryTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    public enum Op
    {
        Read,
        ReadHistory,
        Write
    }

    private const string Arc = "/api/v1/property/architectural-applications";
    private static readonly string Id = Guid.NewGuid().ToString();
    private static readonly string Id2 = Guid.NewGuid().ToString();

    public static IEnumerable<object[]> Endpoints() =>
    [
        // US3-S1 / US3-S8 reads (FR-012a).
        ["GET", "/api/v1/dashboard", Op.Read, null],
        ["GET", "/api/v1/property", Op.Read, null],
        ["GET", "/api/v1/property/owner", Op.Read, null],
        ["GET", "/api/v1/property/address-history", Op.Read, null],
        ["GET", "/api/v1/property/directory-fields", Op.Read, null],
        ["GET", "/api/v1/community/announcements", Op.Read, null],
        ["GET", $"/api/v1/community/announcements/{Id}", Op.Read, null],
        ["GET", "/api/v1/community/events", Op.Read, null],
        ["GET", "/api/v1/community/documents", Op.Read, null],
        ["GET", $"/api/v1/community/documents/{Id}/download", Op.Read, null],
        ["GET", "/api/v1/community/poll", Op.Read, null],
        ["GET", "/api/v1/community/directory", Op.Read, null],
        ["GET", "/api/v1/community/violations", Op.Read, null],
        ["GET", "/api/v1/payments/options", Op.Read, null],
        ["GET", "/api/v1/payments/recurring", Op.Read, null],
        ["GET", "/api/v1/payments/drafts", Op.Read, null],
        ["GET", "/api/v1/payments/alert-preferences", Op.Read, null],
        ["GET", $"{Arc}/drafts/{Id}", Op.Read, null],
        ["GET", $"{Arc}/drafts/{Id}/attachments/{Id2}/url", Op.Read, null],
        // Payment history and submitted architectural requests stay readable when Archived.
        ["GET", "/api/v1/payments/ledger?page=1&pageSize=20", Op.ReadHistory, null],
        ["GET", "/api/v1/payments/statements", Op.ReadHistory, null],
        ["GET", "/api/v1/payments/unpaid-assessments", Op.ReadHistory, null],
        ["GET", "/api/v1/payments/transactions", Op.ReadHistory, null],
        ["GET", $"/api/v1/payments/receipts/{Id}", Op.ReadHistory, null],
        ["GET", Arc, Op.ReadHistory, null],
        ["GET", $"{Arc}/{Id}", Op.ReadHistory, null],
        ["GET", $"{Arc}/{Id}/attachments/{Id2}/url", Op.ReadHistory, null],
        // Writes (FR-012a), including US3-S9's enroll and pay.
        ["PATCH", "/api/v1/property/owner", Op.Write, "{}"],
        ["PATCH", "/api/v1/property/directory-fields/phone", Op.Write, """{"shared":true}"""],
        ["POST", $"/api/v1/community/events/{Id}/rsvp", Op.Write, """{"status":"going"}"""],
        ["POST", $"/api/v1/community/poll/{Id}/vote", Op.Write, """{"optionIndex":0}"""],
        ["POST", "/api/v1/payments/intent", Op.Write, """{"amount":10,"method":"card"}"""],
        ["POST", "/api/v1/payments/one-time/confirm", Op.Write, """{"paymentIntentId":"pi_test"}"""],
        ["POST", "/api/v1/payments/recurring/setup-intent", Op.Write, "{}"],
        ["PUT", "/api/v1/payments/recurring", Op.Write, """{"amountType":"balance","draftDay":1,"setupIntentId":"seti_test","mandateAccepted":true}"""],
        ["DELETE", "/api/v1/payments/recurring", Op.Write, null],
        ["PUT", "/api/v1/payments/alert-preferences", Op.Write, """{"smsOptIn":false,"emailOptIn":false}"""],
        ["POST", $"{Arc}/drafts", Op.Write, "{}"],
        ["PUT", $"{Arc}/drafts/{Id}", Op.Write, "{}"],
        ["DELETE", $"{Arc}/drafts/{Id}", Op.Write, null],
        ["POST", $"{Arc}/drafts/{Id}/submit", Op.Write, null],
        ["POST", $"{Arc}/drafts/{Id}/attachments", Op.Write, "file"],
        ["DELETE", $"{Arc}/drafts/{Id}/attachments/{Id2}", Op.Write, null],
        ["POST", $"{Arc}/{Id}/info-requests/{Id2}/attachments", Op.Write, "file"],
        ["POST", $"{Arc}/{Id}/info-requests/{Id2}/reply", Op.Write, """{"responseMessage":"Here are the plans."}"""],
        ["POST", $"{Arc}/{Id}/withdraw", Op.Write, null],
        ["POST", $"{Arc}/{Id}/revise", Op.Write, null],
    ];

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EveryResidentEndpoint_RechecksTheLinkAndTheCommunityStatus(
        string method, string path, Op op, string? body)
    {
        var companyId = await CreateCompanyAsync();
        var active = await CreateCommunityInAsync(companyId, CommunityStatus.Active);
        var onboarding = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var archived = await CreateCommunityInAsync(companyId, CommunityStatus.Archived);
        var activeHome = await CreatePropertyAsync(active);
        var onboardingHome = await CreatePropertyAsync(onboarding);
        var archivedHome = await CreatePropertyAsync(archived);
        var notMine = await CreatePropertyAsync(active);
        var (userId, _) = await CreateResidentAsync(activeHome);
        await LinkResidentAsync(userId, onboardingHome);
        await LinkResidentAsync(userId, archivedHome);

        var untouched = new[] { onboardingHome, archivedHome, notMine };
        var before = await FingerprintAsync(userId, untouched);

        // US9-S4 / US8-S6: no home claim, or a home the caller isn't linked to → 403, nothing changed.
        await AssertRefusedAsync(method, path, body, MintToken(userId), HttpStatusCode.Forbidden, "PROPERTY_ACCESS_DENIED");
        await AssertRefusedAsync(method, path, body, MintToken(userId, notMine, active), HttpStatusCode.Forbidden, "PROPERTY_ACCESS_DENIED");

        // US3-S1: an Onboarding home is refused for every read and write.
        await AssertRefusedAsync(method, path, body, MintToken(userId, onboardingHome, onboarding), HttpStatusCode.Conflict, "COMMUNITY_NOT_ACTIVE");

        // US3-S8 / US3-S9: an Archived home keeps payment history and submitted requests; all else is refused.
        var archivedToken = MintToken(userId, archivedHome, archived);
        if (op == Op.ReadHistory)
            await AssertNotRefusedAsync(method, path, body, archivedToken);
        else
            await AssertRefusedAsync(method, path, body, archivedToken, HttpStatusCode.Conflict, "COMMUNITY_ARCHIVED");

        Assert.Equal(before, await FingerprintAsync(userId, untouched));

        // A linked resident of an Active home is not refused.
        await AssertNotRefusedAsync(method, path, body, MintToken(userId, activeHome, active));
    }

    // FR-012a "never from the token": the endpoint runs with the database's community.
    [Fact]
    public async Task WrongCommunityClaim_StillReadsTheHomesRealCommunity()
    {
        var companyId = await CreateCompanyAsync();
        var active = await CreateCommunityInAsync(companyId);
        var archived = await CreateCommunityInAsync(companyId, CommunityStatus.Archived);
        var home = await CreatePropertyAsync(active);
        var (userId, _) = await CreateResidentAsync(home);
        var title = $"Pool reopens {Guid.NewGuid():N}";
        await WithDbAsync(async db =>
        {
            db.Announcements.Add(new Announcement
            {
                Id = Guid.NewGuid(), CommunityId = active, Title = title, Body = "Saturday.",
                PublishedAt = DateTimeOffset.UtcNow, Category = AnnouncementCategory.Events, AuthorName = "Board",
            });
            await db.SaveChangesAsync();
        });

        using var client = ClientWithToken(MintToken(userId, home, archived));
        var res = await client.GetAsync("/api/v1/community/announcements");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains(title, await res.Content.ReadAsStringAsync());
    }

    private async Task AssertRefusedAsync(string method, string path, string? body, string token,
        HttpStatusCode status, string code)
    {
        using var client = ClientWithToken(token);
        using var res = await client.SendAsync(Request(method, path, body));
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == status, $"{method} {path}: expected {(int)status} {code}, got {(int)res.StatusCode} {text}");
        Assert.Equal(code, JsonDocument.Parse(text).RootElement.GetProperty("code").GetString());
    }

    private async Task AssertNotRefusedAsync(string method, string path, string? body, string token)
    {
        using var client = ClientWithToken(token);
        using var res = await client.SendAsync(Request(method, path, body));
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode != HttpStatusCode.Unauthorized, $"{method} {path}: 401 {text}");
        Assert.True(res.StatusCode < HttpStatusCode.InternalServerError, $"{method} {path}: {(int)res.StatusCode} {text}");
        foreach (var refusal in new[] { "PROPERTY_ACCESS_DENIED", "COMMUNITY_NOT_ACTIVE", "COMMUNITY_ARCHIVED" })
            Assert.False(text.Contains($"\"{refusal}\""), $"{method} {path}: refused with {refusal}: {text}");
    }

    private static HttpRequestMessage Request(string method, string path, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body == "file")
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%test\n"));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", "plans.pdf");
            request.Content = form;
        }
        else if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    // Counts of everything a resident write could add for the homes that must stay untouched.
    private Task<string> FingerprintAsync(string userId, Guid[] properties) => WithDbAsync(async db => string.Join("|",
        await db.PollVotes.CountAsync(v => v.UserId == userId),
        await db.EventRsvps.CountAsync(r => r.UserId == userId),
        await db.RecurringPayments.CountAsync(r => properties.Contains(r.PropertyId)),
        await db.PaymentTransactions.CountAsync(t => properties.Contains(t.PropertyId)),
        await db.ArchitecturalApplicationDrafts.CountAsync(d => properties.Contains(d.PropertyId)),
        await db.AlertConsents.CountAsync()));
}
