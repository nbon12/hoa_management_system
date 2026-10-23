using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Board;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// Shared harness for 030 company administration tests (T017). Every helper creates only its own
/// rows, keyed by fresh GUIDs, and tests that need a company create their own, so tests are
/// order-independent and safe against prior-run data. Isolation follows the pattern recorded in
/// plan.md's Complexity Tracking: unique rows per test rather than a rolled-back transaction,
/// because the API commits on its own connections (testing constitution §2.1, approved 2026-10-18).
/// </summary>
public abstract class CompanyTestBase(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    // Starts on a whole microsecond, the precision Postgres keeps, so stored times compare equal.
    protected TestClock Clock { get; } = new() { UtcNow = new DateTimeOffset(DateTimeOffset.UtcNow.UtcTicks / 10 * 10, TimeSpan.Zero) };
    protected FakeAlertProvider Email { get; } = new("email");
    protected FakeAlertProvider Sms { get; } = new("sms");
    protected FakeStripeGateway Stripe { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IStripeGateway>();
        services.AddSingleton<IStripeGateway>(Stripe);
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Clock);
        services.RemoveAll<IAlertProvider>();
        services.AddSingleton<IAlertProvider>(Sms);
        services.AddSingleton<IAlertProvider>(Email);
    }

    protected async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> f)
    {
        using var scope = NewScope();
        return await f(Db(scope));
    }

    protected async Task WithDbAsync(Func<ApplicationDbContext, Task> f)
    {
        using var scope = NewScope();
        await f(Db(scope));
    }

    /// <summary>A new management company with the 027 default settings.</summary>
    protected Task<Guid> CreateCompanyAsync(string? name = null) => WithDbAsync(async db =>
    {
        var id = Guid.NewGuid();
        db.ManagementCompanies.Add(new ManagementCompany
        {
            Id = id,
            Name = name ?? $"Test Company {id:N}",
            DefaultSettings = new CompanyDefaultSettings { CompanyId = id },
        });
        await db.SaveChangesAsync();
        return id;
    });

    /// <summary>A user (no home, password <see cref="BoardTestBase.Password"/>) holding a live Company Administrator grant.</summary>
    protected Task<(string UserId, string Email)> CreateCompanyAdminAsync(Guid companyId) => WithDbAsync(async db =>
    {
        var userId = await CreateUserAsync(db);
        db.CompanyAdministratorGrants.Add(new CompanyAdministratorGrant
        {
            Id = Guid.NewGuid(), CompanyId = companyId, UserId = userId,
            Source = CompanyGrantSource.Setup, GrantedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return (userId, EmailOf(userId));
    });

    /// <summary>A community in <paramref name="companyId"/>, with its own ARC settings row as 030 creates them.</summary>
    protected Task<Guid> CreateCommunityInAsync(
        Guid companyId, CommunityStatus status = CommunityStatus.Active, string? name = null,
        Guid? parentId = null, string? legalName = null) => WithDbAsync(async db =>
    {
        var id = Guid.NewGuid();
        db.Communities.Add(new Domain.Entities.Community
        {
            Id = id,
            ManagementCompanyId = companyId,
            CommunityName = name ?? $"community-{id:N}",
            LegalName = legalName ?? $"Community {id:N}, Inc.",
            Status = status,
            ParentCommunityId = parentId,
            ActivatedAt = status == CommunityStatus.Onboarding ? null : DateTimeOffset.UtcNow.AddMonths(-1),
            ArchivedAt = status == CommunityStatus.Archived ? DateTimeOffset.UtcNow : null,
        });
        db.CommunityArcSettings.Add(CommunityArcSettings.Defaults(id));
        await db.SaveChangesAsync();
        return id;
    });

    /// <summary>A user (no home) holding <paramref name="role"/> in <paramref name="communityId"/>.</summary>
    protected Task<(string UserId, string Email)> CreateStaffAsync(
        Guid communityId, CommunityRole role, DateOnly? endDate = null,
        MembershipStatus status = MembershipStatus.Active) => WithDbAsync(async db =>
    {
        var userId = await CreateUserAsync(db);
        await AddMembershipAsync(db, userId, communityId, role, status, endDate: endDate);
        return (userId, EmailOf(userId));
    });

    protected Task<(string UserId, string Email)> CreateManagerAsync(Guid communityId, DateOnly? endDate = null) =>
        CreateStaffAsync(communityId, CommunityRole.CommunityManager, endDate);

    /// <summary>A property in <paramref name="communityId"/> with its owner of record (no resident yet).</summary>
    protected Task<Guid> CreatePropertyAsync(Guid communityId, string? address = null) => WithDbAsync(async db =>
    {
        var id = Guid.NewGuid();
        db.Properties.Add(new Domain.Entities.Property
        {
            Id = id, AccountNumber = $"CO-{id:N}", CommunityId = communityId,
            Address = address ?? $"{Random.Shared.Next(1, 999)} Keystone Park Dr", City = "Raleigh", State = "NC",
            Zip = "27601", Lot = "1", Section = "1", FiscalYear = 2026, YearBuilt = 2005, Status = "active",
            MonthlyAssessment = 250m, AnnualAssessment = 3000m, AssessmentDueDay = 1, LateFeeAmount = 50m,
            LateFeeGraceDays = 15, FinanceChargeRate = 0.015m,
        });
        db.Owners.Add(new Owner
        {
            Id = Guid.NewGuid(), PropertyId = id, FirstName = "Pat", LastName = "Owner", Email = $"owner-{id:N}@nekohoa.dev",
        });
        await db.SaveChangesAsync();
        return id;
    });

    /// <summary>A new user linked to <paramref name="propertyId"/> as a resident.</summary>
    protected Task<(string UserId, string Email)> CreateResidentAsync(Guid propertyId) => WithDbAsync(async db =>
    {
        var userId = await CreateUserAsync(db);
        db.UserProperties.Add(new UserProperty { Id = Guid.NewGuid(), UserId = userId, PropertyId = propertyId });
        await db.SaveChangesAsync();
        return (userId, EmailOf(userId));
    });

    protected Task LinkResidentAsync(string userId, Guid propertyId) => WithDbAsync(async db =>
    {
        db.UserProperties.Add(new UserProperty { Id = Guid.NewGuid(), UserId = userId, PropertyId = propertyId });
        await db.SaveChangesAsync();
    });

    /// <summary>An active autopay schedule for <paramref name="propertyId"/> with a current authorization.</summary>
    protected Task<Guid> CreateAutopayAsync(Guid propertyId, string? enrolledBy) => WithDbAsync(async db =>
    {
        var id = Guid.NewGuid();
        var authorizationId = Guid.NewGuid();
        db.RecurringPayments.Add(new RecurringPayment
        {
            Id = id, PropertyId = propertyId, AmountType = RecurringAmountType.Balance, Method = PaymentMethod.Card,
            DraftDay = 1, Status = "active", EnrolledByUserId = enrolledBy, CurrentAuthorizationId = authorizationId,
            VaultedPaymentMethodId = "pm_test", MethodBrand = "visa", MethodLast4 = "4242",
        });
        db.PaymentAuthorizations.Add(new PaymentAuthorization
        {
            Id = authorizationId, RecurringPaymentId = id, MandateText = "test mandate", MandateVersion = "v1",
            AmountTermsSnapshot = "balance",
        });
        await db.SaveChangesAsync();
        return id;
    });

    /// <summary>An architectural application row, so lifecycle tests can arrange open work (FR-014).</summary>
    protected Task<Guid> CreateArcApplicationAsync(Guid communityId, Guid propertyId, int number,
        ArcApplicationStatus status = ArcApplicationStatus.Open) => WithDbAsync(async db =>
    {
        var received = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddDays(-5);
        var reached = status != ArcApplicationStatus.Open;
        var app = new ArchitecturalApplication
        {
            Id = Guid.NewGuid(), CommunityId = communityId, PropertyId = propertyId, ApplicationNumber = number,
            Revision = 1, OwnerName = "Pat Owner", ProjectType = ArcProjectType.Fence, ProjectTitle = "Fence replacement",
            Description = "Replace the rear fence.", ReceivedDate = received, DueDate = received.AddDays(30),
            DecisionRule = ArcDecisionRule.MajorityOfMembers, LapseRule = ArcLapseRule.FlagOverdueOnly,
            TimeZoneId = CommunityArcSettings.DefaultTimeZoneId, Status = status,
            DecisionOutcome = reached ? ArcOutcome.Approved : null,
            DecisionSource = reached ? ArcDecisionSource.Votes : null,
            DecisionReachedAt = reached ? Clock.UtcNow : null,
            ClosedAt = status == ArcApplicationStatus.Closed ? Clock.UtcNow : null,
        };
        db.ArchitecturalApplications.Add(app);
        await db.SaveChangesAsync();
        return app.Id;
    });

    /// <summary>
    /// A residency invitation row, inserted directly (the Residency offer arrives with US8). A
    /// Pending one gets a fresh link; the token is returned.
    /// </summary>
    protected Task<(Guid Id, string? Token)> InsertResidencyInvitationAsync(Guid companyId, Guid communityId, Guid propertyId,
        string email, string senderId, InvitationState state) => WithDbAsync(async db =>
    {
        var token = state == InvitationState.Pending ? Features.Invitations.InvitationTokens.New() : null;
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(), Kind = InvitationKind.Residency, CompanyId = companyId, CommunityId = communityId,
            PropertyId = propertyId, Email = email, NormalizedEmail = Invitation.Normalize(email), State = state,
            SenderUserId = senderId, CreatedAt = Clock.UtcNow,
            TokenHash = token is null ? null : Features.Invitations.InvitationTokens.Hash(token),
            SentAt = token is null ? null : Clock.UtcNow, ExpiresAt = token is null ? null : Clock.UtcNow.AddHours(72),
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();
        return (invitation.Id, token);
    });

    /// <summary>Signs <paramref name="email"/> in on a new client and returns it with the bearer header set.</summary>
    protected async Task<HttpClient> ClientForAsync(string email, string password = Password)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!["token"].GetString());
        return client;
    }

    /// <summary>
    /// Signs an access token the way AuthService does, for any home — including one the user is no
    /// longer linked to, or none — so tests can play a stale or forged token (FR-024g).
    /// </summary>
    protected string MintToken(string userId, Guid? propertyId = null, Guid? communityId = null)
    {
        var config = Services.GetRequiredService<IConfiguration>();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, EmailOf(userId)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (propertyId is { } p) claims.Add(new Claim("propertyId", p.ToString()));
        if (communityId is { } c) claims.Add(new Claim("communityId", c.ToString()));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims,
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected HttpClient ClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>The email <see cref="BoardTestBase.CreateUserAsync"/> gives a user.</summary>
    protected static string EmailOf(string userId) => $"{userId}@nekohoa.dev";

    protected static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>())!;
}
