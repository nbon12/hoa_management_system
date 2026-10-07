using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>
/// Hand-written controllable clock (no Microsoft.Extensions.TimeProvider.Testing package — 027 plan
/// "no new packages"). Registered as the app's <see cref="TimeProvider"/> for every ARC test.
/// </summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}

/// <summary>
/// Shared harness for 027 architectural review tests (T020). Every helper creates only its own rows,
/// keyed by fresh GUIDs, so tests are order-independent and safe against prior-run data.
/// </summary>
/// <summary>Exposes the ARC property factory to non-ARC test classes (e.g. the seeder tests).</summary>
public static class ArcTestBaseAccess
{
    public static Task<Guid> CreatePropertyAsync(ApplicationDbContext db, Guid communityId) =>
        ArcTestBase.CreatePropertyStaticAsync(db, communityId);
}

public abstract class ArcTestBase(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    protected TestClock Clock { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Clock);
    }

    protected sealed record BoardMember(string UserId, string Email);

    protected sealed record ArcScenario(Guid CommunityId, IReadOnlyList<BoardMember> Board, Guid PropertyId);

    /// <summary>
    /// A community with <paramref name="boardSize"/> login-capable board members (each linked to their
    /// own property, so none is recused) and one application property owned by nobody on the board.
    /// </summary>
    protected async Task<ArcScenario> CreateScenarioAsync(int boardSize, bool ownerWithEmail = true)
    {
        using var scope = NewScope();
        var db = Db(scope);
        var communityId = await CreateCommunityAsync(db);
        var board = new List<BoardMember>();
        for (var i = 0; i < boardSize; i++)
        {
            var (userId, email, _) = await CreateUserWithPropertyAsync(db, communityId);
            await AddMembershipAsync(db, userId, communityId, CommunityRole.BoardMember);
            board.Add(new BoardMember(userId, email));
        }
        var propertyId = await CreatePropertyAsync(db, communityId, ownerWithEmail);
        return new ArcScenario(communityId, board, propertyId);
    }

    protected async Task<BoardMember> CreateMemberAsync(Guid communityId, CommunityRole role)
    {
        using var scope = NewScope();
        var db = Db(scope);
        var (userId, email, _) = await CreateUserWithPropertyAsync(db, communityId);
        await AddMembershipAsync(db, userId, communityId, role);
        return new BoardMember(userId, email);
    }

    protected static Task<Guid> CreatePropertyAsync(ApplicationDbContext db, Guid communityId, bool ownerWithEmail = true,
        string address = "711 Keystone Park Dr #29") => CreatePropertyStaticAsync(db, communityId, ownerWithEmail, address);

    internal static async Task<Guid> CreatePropertyStaticAsync(ApplicationDbContext db, Guid communityId, bool ownerWithEmail = true,
        string address = "711 Keystone Park Dr #29")
    {
        var id = Guid.NewGuid();
        db.Properties.Add(new Domain.Entities.Property
        {
            Id = id, AccountNumber = $"ARC-{id:N}", CommunityId = communityId, Address = address,
            City = "Raleigh", State = "NC", Zip = "27560", Lot = "L1", Section = "1", FiscalYear = 2026,
            YearBuilt = 2005, Status = "active", MonthlyAssessment = 100m, AnnualAssessment = 1200m,
            AssessmentDueDay = 1, LateFeeAmount = 25m, LateFeeGraceDays = 15, FinanceChargeRate = 0.01m,
        });
        if (ownerWithEmail)
            db.Owners.Add(new Owner
            {
                Id = Guid.NewGuid(), PropertyId = id, FirstName = "Praneeth", LastName = "Pattyam",
                Email = $"owner-{id:N}@example.com"
            });
        await db.SaveChangesAsync();
        return id;
    }

    protected sealed record AppSpec
    {
        public int Number { get; init; } = Random.Shared.Next(100_000, 999_999);
        public int Revision { get; init; } = 1;
        public Guid? PreviousRevisionId { get; init; }
        public string OwnerName { get; init; } = "Praneeth Pattyam";
        public string Title { get; init; } = "Fence replacement — 6ft cedar";
        public DateOnly? Received { get; init; }
        public DateOnly? Due { get; init; }
        public ArcApplicationStatus Status { get; init; } = ArcApplicationStatus.Open;
        public ArcDecisionRule DecisionRule { get; init; } = ArcDecisionRule.MajorityOfMembers;
        public ArcLapseRule LapseRule { get; init; } = ArcLapseRule.FlagOverdueOnly;
        public string TimeZoneId { get; init; } = "America/New_York";
        public ArcOutcome? Outcome { get; init; }
        public ArcDenialWording? Wording { get; init; }
        public (string Name, long Size)[] Attachments { get; init; } = [];
    }

    /// <summary>Inserts an application row directly so each test controls status, dates and rules.</summary>
    protected async Task<Guid> CreateApplicationAsync(ArcScenario s, AppSpec? spec = null, Guid? propertyId = null)
    {
        spec ??= new AppSpec();
        using var scope = NewScope();
        var db = Db(scope);
        var today = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime);
        var received = spec.Received ?? today.AddDays(-5);
        var app = new ArchitecturalApplication
        {
            Id = Guid.NewGuid(),
            CommunityId = s.CommunityId,
            PropertyId = propertyId ?? s.PropertyId,
            ApplicationNumber = spec.Number,
            Revision = spec.Revision,
            PreviousRevisionId = spec.PreviousRevisionId,
            OwnerName = spec.OwnerName,
            ProjectType = ArcProjectType.Fence,
            ProjectTitle = spec.Title,
            Description = "Replace the rear fence.",
            ReceivedDate = received,
            DueDate = spec.Due ?? received.AddDays(30),
            DecisionRule = spec.DecisionRule,
            LapseRule = spec.LapseRule,
            TimeZoneId = spec.TimeZoneId,
            Status = spec.Status,
            DecisionOutcome = spec.Outcome,
            DecisionWording = spec.Wording,
            DecisionSource = spec.Outcome is null ? null : ArcDecisionSource.Votes,
            DecisionReachedAt = spec.Outcome is null ? null : DateTimeOffset.UtcNow,
            ClosedAt = spec.Status == ArcApplicationStatus.Closed ? DateTimeOffset.UtcNow : null,
        };
        foreach (var (name, size) in spec.Attachments)
            app.Attachments.Add(new ArchitecturalAttachment
            {
                FileName = name, SizeBytes = size,
                ContentType = name.EndsWith(".pdf") ? "application/pdf" : "image/jpeg",
                StorageKey = $"arc/{s.CommunityId}/{spec.Number}/{Guid.NewGuid()}"
            });
        db.ArchitecturalApplications.Add(app);
        await db.SaveChangesAsync();
        return app.Id;
    }

    /// <summary>Records votes directly (bypassing the API) to arrange a tally.</summary>
    protected async Task AddVotesAsync(Guid applicationId, params (BoardMember Voter, ArcVoteChoice Choice)[] votes)
    {
        using var scope = NewScope();
        var db = Db(scope);
        foreach (var (voter, choice) in votes)
            db.ArchitecturalVotes.Add(new ArchitecturalVote
            {
                ApplicationId = applicationId, VoterUserId = voter.UserId, Choice = choice
            });
        await db.SaveChangesAsync();
    }

    protected async Task LinkUserToPropertyAsync(string userId, Guid propertyId)
    {
        using var scope = NewScope();
        var db = Db(scope);
        db.UserProperties.Add(new UserProperty { Id = Guid.NewGuid(), UserId = userId, PropertyId = propertyId });
        await db.SaveChangesAsync();
    }

    protected Task LoginAsAsync(BoardMember member) => LoginAsync(member.Email);

    protected static string AppsUrl(Guid communityId) => $"/api/v1/communities/{communityId}/architectural-applications";

    protected static string AppUrl(Guid communityId, Guid applicationId) => $"{AppsUrl(communityId)}/{applicationId}";

    protected async Task<ArcListResponse> ListAsync(Guid communityId, string query = "")
    {
        var res = await Client.GetAsync(AppsUrl(communityId) + query);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ArcListResponse>(Json))!;
    }

    protected async Task<ArcDetailDto> DetailAsync(Guid communityId, Guid applicationId)
    {
        var res = await Client.GetAsync(AppUrl(communityId, applicationId));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ArcDetailDto>(Json))!;
    }

    protected Task<HttpResponseMessage> VoteAsync(Guid communityId, Guid applicationId, string choice, string? comment = null) =>
        Client.PostAsJsonAsync($"{AppUrl(communityId, applicationId)}/votes", new { choice, comment });

    protected static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    protected async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> f)
    {
        using var scope = NewScope();
        return await f(Db(scope));
    }

    protected HttpClient ClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
