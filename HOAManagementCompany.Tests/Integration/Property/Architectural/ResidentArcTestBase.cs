using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Property.Architectural;
using HOAManagementCompany.Infrastructure.Storage;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Board.Architectural;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>
/// Shared harness for 029 resident architectural-request tests (T020). Builds on 027's
/// <see cref="ArcTestBase"/> (Testcontainers PostgreSQL + MinIO, a controllable clock). Each
/// resident gets a fresh community, property, Owner row and login-capable user linked by
/// <c>UserProperty</c>, so the login token carries that property as the active <c>propertyId</c>
/// claim. Every test creates its own rows, which keeps tests order-independent and parallel-safe.
/// </summary>
public abstract class ResidentArcTestBase(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    protected const string Base = "/api/v1/property/architectural-applications";

    protected sealed record Resident(string UserId, string Email, Guid PropertyId, Guid CommunityId, HttpClient Http);

    /// <summary>A resident who owns a property. Pass a property to create a co-owner of it.</summary>
    protected async Task<Resident> CreateResidentAsync(Guid? communityId = null, Guid? propertyId = null)
    {
        Guid community;
        Guid property;
        string userId;
        string email;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            community = communityId ?? await CreateCommunityAsync(db);
            property = propertyId ?? await CreatePropertyAsync(db, community);
            userId = await CreateUserAsync(db);
            email = (await db.Users.FindAsync(userId))!.Email!;
        }
        await LinkUserToPropertyAsync(userId, property);
        return new Resident(userId, email, property, community, await LoggedInClientAsync(email));
    }

    /// <summary>A second owner linked to the same property (co-owner parity, FR-022).</summary>
    protected Task<Resident> AddCoOwnerAsync(Resident owner) => CreateResidentAsync(owner.CommunityId, owner.PropertyId);

    /// <summary>A logged-in client for a new member of <paramref name="communityId"/> with <paramref name="role"/>.</summary>
    protected async Task<(BoardMember Member, HttpClient Http)> CreateBoardClientAsync(
        Guid communityId, CommunityRole role = CommunityRole.BoardMember)
    {
        var member = await CreateMemberAsync(communityId, role);
        return (member, await LoggedInClientAsync(member.Email));
    }

    protected async Task<HttpClient> LoggedInClientAsync(string email)
    {
        var anon = CreateClient();
        var res = await anon.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>())!["token"].GetString()!;
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return anon;
    }

    /// <summary>Gives a community explicit ARC settings, so tests can assert snapshots against non-defaults.</summary>
    protected async Task SetArcSettingsAsync(Guid communityId, int nextNumber = 1042, int reviewDays = 45,
        string timeZone = "America/Chicago", ArcDecisionRule rule = ArcDecisionRule.MajorityOfVotesCastWithQuorum,
        ArcLapseRule lapse = ArcLapseRule.DeemedApproved, string statement = "This is a formal disapproval under §12.3.")
    {
        using var scope = NewScope();
        var db = Db(scope);
        db.CommunityArcSettings.Add(new CommunityArcSettings
        {
            CommunityId = communityId, NextApplicationNumber = nextNumber, ReviewPeriodDays = reviewDays,
            TimeZoneId = timeZone, DecisionRule = rule, LapseRule = lapse, FormalDisapprovalStatement = statement
        });
        await db.SaveChangesAsync();
    }

    // ── Request bodies ───────────────────────────────────────────────────────

    protected static readonly DateOnly Start = new(2026, 11, 2);
    protected static readonly DateOnly Finish = new(2026, 11, 20);

    protected static Dictionary<string, object?> CompleteDraft(bool acknowledged = true) => new()
    {
        ["projectType"] = "Fence",
        ["projectTitle"] = "Fence replacement — 6ft cedar",
        ["description"] = "Replace the rear fence with 6ft cedar on the existing line.",
        ["plannedStartDate"] = Start,
        ["plannedCompletionDate"] = Finish,
        ["contractorName"] = "Cedar & Co",
        ["contractorContact"] = "919-555-0100",
        ["acknowledged"] = acknowledged
    };

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<T>(Json))!;

    protected async Task<ResidentArcDraftDto> CreateDraftAsync(Resident r, Dictionary<string, object?>? body = null)
    {
        var res = await r.Http.PostAsJsonAsync($"{Base}/drafts", body ?? CompleteDraft());
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await ReadAsync<ResidentArcDraftDto>(res);
    }

    protected Task<HttpResponseMessage> SubmitAsync(Resident r, Guid draftId) =>
        r.Http.PostAsync($"{Base}/drafts/{draftId}/submit", null);

    /// <summary>Creates a complete draft (optionally with one PDF) and submits it. Returns the detail.</summary>
    protected async Task<ResidentArcDetailDto> SubmitNewAsync(Resident r, bool withPdf = false)
    {
        var draft = await CreateDraftAsync(r);
        if (withPdf)
            Assert.Equal(HttpStatusCode.Created,
                (await UploadAsync(r.Http, $"{Base}/drafts/{draft.Id}/attachments", ValidPdf, "fence-plan.pdf")).StatusCode);
        var res = await SubmitAsync(r, draft.Id);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await ReadAsync<ResidentArcDetailDto>(res);
    }

    protected async Task<ResidentArcDetailDto> DetailOfAsync(Resident r, Guid applicationId)
    {
        var res = await r.Http.GetAsync($"{Base}/{applicationId}");
        res.EnsureSuccessStatusCode();
        return await ReadAsync<ResidentArcDetailDto>(res);
    }

    protected async Task<ResidentArcListResponse> MyListAsync(Resident r, string query = "")
    {
        var res = await r.Http.GetAsync(Base + query);
        res.EnsureSuccessStatusCode();
        return await ReadAsync<ResidentArcListResponse>(res);
    }

    protected static Task<HttpResponseMessage> UploadAsync(HttpClient http, string url, byte[] bytes, string fileName,
        string declaredType = "application/pdf")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(declaredType);
        content.Add(file, "file", fileName);
        return http.PostAsync(url, content);
    }

    /// <summary>Arranges an application row directly on the resident's property (states the API can't reach).</summary>
    protected Task<Guid> SeedApplicationAsync(Resident r, AppSpec spec) =>
        CreateApplicationAsync(new ArcScenario(r.CommunityId, [], r.PropertyId), spec, r.PropertyId);

    protected async Task<Guid> SeedInfoRequestAsync(Guid applicationId, string requestedByUserId, string message,
        DateTimeOffset? at = null, DateTimeOffset? respondedAt = null)
    {
        using var scope = NewScope();
        var db = Db(scope);
        var info = new ArchitecturalInfoRequest
        {
            ApplicationId = applicationId, RequestedByUserId = requestedByUserId, Message = message,
            RequestedAt = at ?? Clock.UtcNow, RespondedAt = respondedAt
        };
        db.ArchitecturalInfoRequests.Add(info);
        await db.SaveChangesAsync();
        return info.Id;
    }

    protected async Task<bool> ObjectExistsAsync(string key)
    {
        using var scope = NewScope();
        return await scope.ServiceProvider.GetRequiredService<IDocumentStorage>().ExistsAsync(key);
    }

    protected async Task PutObjectAsync(string key, byte[] bytes, string contentType = "application/pdf")
    {
        using var scope = NewScope();
        await scope.ServiceProvider.GetRequiredService<IDocumentStorage>().UploadAsync(key, bytes, contentType);
    }

    protected static async Task AssertErrorAsync(HttpResponseMessage res, HttpStatusCode status, string code)
    {
        Assert.Equal(status, res.StatusCode);
        Assert.Equal(code, await ErrorCodeAsync(res));
    }

    /// <summary>The exact non-disclosing 403 body from the contract.</summary>
    protected static async Task AssertForbiddenAsync(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("FORBIDDEN", body.GetProperty("code").GetString());
        Assert.Equal(ResidentArcScope.ForbiddenMessage, body.GetProperty("message").GetString());
    }

    protected DateOnly TodayIn(string timeZoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).DateTime);

    // ── File fixtures: real magic numbers, padded to a realistic size ─────────

    protected static byte[] Padded(byte[] header, int size = 2048) => [.. header, .. new byte[Math.Max(0, size - header.Length)]];
    protected static byte[] ValidPdf => Padded(Encoding.ASCII.GetBytes("%PDF-1.7\n%placeholder\n"));
    protected static byte[] ValidJpeg => Padded([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F']);
    protected static byte[] ValidPng => Padded([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]);
    protected static byte[] ValidHeic => Padded([0x00, 0x00, 0x00, 0x18, .. "ftypheic"u8.ToArray(), 0x00, 0x00, 0x00, 0x00]);
    protected static byte[] ExeRenamedPdf => Padded([(byte)'M', (byte)'Z', 0x90, 0x00, 0x03, 0x00, 0x00, 0x00]);
    protected static byte[] TextRenamedJpg => Padded(Encoding.ASCII.GetBytes("definitely not a photograph"));
}
