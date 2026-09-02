using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Factories;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// Shared harness for the 025-board-overall-design integration tests. Every helper
/// creates fully isolated rows keyed by <see cref="Guid.NewGuid"/> so tests in this
/// (serial) collection never collide with the shared seeded residents or with each
/// other, honouring the constitution's parallel-safe / order-independent requirement.
/// </summary>
public abstract class BoardTestBase(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    protected const string Password = "Password1!";

    // Client-side deserialization: the API serializes camelCase; be tolerant of case.
    protected static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    protected IServiceScope NewScope() => Services.CreateScope();

    protected static ApplicationDbContext Db(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    /// <summary>Creates an active (by default) community with a globally-unique name.</summary>
    protected static async Task<Guid> CreateCommunityAsync(
        ApplicationDbContext db,
        CommunityStatus status = CommunityStatus.Active,
        Guid? parentCommunityId = null)
    {
        var id = Guid.NewGuid();
        db.Communities.Add(new Domain.Entities.Community
        {
            Id = id,
            LegalName = $"Test Community {id} LLC",
            CommunityName = $"test-community-{id:N}",
            Status = status,
            ParentCommunityId = parentCommunityId,
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>
    /// Creates a login-capable user (hashed password <see cref="Password"/>) linked to a
    /// brand-new property in <paramref name="communityId"/>. A linked property is required
    /// because <c>AuthService.LoginAsync</c> mints propertyId/communityId claims from it.
    /// </summary>
    protected static async Task<(string userId, string email, Guid propertyId)> CreateUserWithPropertyAsync(
        ApplicationDbContext db, Guid communityId)
    {
        var userId = "board-test-" + Guid.NewGuid().ToString("N");
        var email = $"{userId}@nekohoa.dev";
        var user = NewUser(userId, email);
        db.Users.Add(user);

        // Account number carries the isolation guarantee (it is uniquely indexed, and
        // ResidentScopeUnchangedTests distinguishes properties by it), so keep it per-row unique.
        var property = PropertyFactory.Create(communityId, $"BT-{Guid.NewGuid():N}");
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        db.UserProperties.Add(new UserProperty { Id = Guid.NewGuid(), UserId = userId, PropertyId = property.Id });
        await db.SaveChangesAsync();
        return (userId, email, property.Id);
    }

    /// <summary>Creates a user with NO property link (cannot log in; used for resolver-only tests).</summary>
    protected static async Task<string> CreateUserAsync(ApplicationDbContext db)
    {
        var userId = "board-test-" + Guid.NewGuid().ToString("N");
        db.Users.Add(NewUser(userId, $"{userId}@nekohoa.dev"));
        await db.SaveChangesAsync();
        return userId;
    }

    protected static async Task<Guid> AddMembershipAsync(
        ApplicationDbContext db, string userId, Guid communityId, CommunityRole role,
        MembershipStatus status = MembershipStatus.Active,
        DateOnly? startDate = null, DateOnly? endDate = null)
    {
        var m = new CommunityMembership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CommunityId = communityId,
            Role = role,
            Status = status,
            StartDate = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
            EndDate = endDate,
        };
        db.CommunityMemberships.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    /// <summary>Logs in and sets the bearer header on <see cref="IntegrationTestBase.Client"/>. Returns the token.</summary>
    protected async Task<string> LoginAsync(string email, string password = Password)
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var token = body!["token"].GetString()!;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token;
    }

    /// <summary>Builds a ClaimsPrincipal carrying the user id as both NameIdentifier and sub.</summary>
    protected static ClaimsPrincipal Principal(string userId) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("sub", userId),
        ], authenticationType: "Test"));

    protected static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static ApplicationUser NewUser(string id, string email)
    {
        var user = new ApplicationUser
        {
            Id = id,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            FirstName = "Board",
            LastName = "Tester",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, Password);
        return user;
    }
}
