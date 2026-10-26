using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T098 — the community profile (US3-S17 to S21; FR-017, R14; contracts/community-settings.md § profile).
/// </summary>
public class CommunityProfileTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private readonly BeforeRenamingACommunity _beforeRename = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.ConfigureDbContext<ApplicationDbContext>(o => o.AddInterceptors(_beforeRename));
    }

    // US3-S17: each of the six fields saves alone and logs its old and new value (an empty old value logs as null).
    [Theory]
    [InlineData("legalName", "Keystone Ridge Homeowners Association, Inc.", "LegalName", "\"Keystone Ridge HOA, Inc.\"")]
    [InlineData("communityName", "Keystone Bluff", "CommunityName", "\"Keystone Ridge\"")]
    [InlineData("county", "Wake", "County", "\"Hamilton\"")]
    [InlineData("formationDate", "2001-05-06", "FormationDate", "\"2004-03-14\"")]
    [InlineData("managementStartDate", "2021-02-01", "ManagementStartDate", "\"2020-01-01\"")]
    [InlineData("description", "A neighborhood of 120 homes.", "Description", "null")]
    public async Task AManager_ChangesOneProfileField_AndOnlyThatFieldChanges_AndTheChangeIsLogged(
        string field, string value, string loggedField, string loggedOld)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateProfiledCommunityAsync(companyId, "Keystone Ridge");
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);
        var before = await ProfileRowAsync(community);

        var res = await manager.PatchAsJsonAsync(Url(community), new Dictionary<string, string> { [field] = value });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(value, (await JsonAsync(res)).GetProperty(field).GetString());
        Assert.Equal(field switch
        {
            "legalName" => before with { LegalName = value },
            "communityName" => before with { CommunityName = value },
            "county" => before with { County = value },
            "formationDate" => before with { FormationDate = DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture) },
            "managementStartDate" => before with { ManagementStartDate = DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture) },
            "description" => before with { Description = value },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        }, await ProfileRowAsync(community));
        var logged = Assert.Single(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CommunityProfileChanged"));
        Assert.Equal(($"\"{managerId}\"", community.ToString(), $"\"{loggedField}\"", loggedOld, $"\"{value}\""),
            (logged.Properties["ActorId"].ToString(), logged.Properties["CommunityId"].ToString(), logged.Properties["Field"].ToString(),
                logged.Properties["Old"].ToString(), logged.Properties["New"].ToString()));
    }

    // US3-S17 (invalid values)
    [Theory]
    [InlineData("formationDate", "{\"formationDate\":\"2099-01-01\"}")]
    [InlineData("legalName", "{\"legalName\":\"LONG\"}")]
    [InlineData("county", "{\"county\":null}")]
    [InlineData("legalName", "{\"legalName\":\"   \"}")]
    public async Task AnInvalidValue_IsRefusedWithItsField_AndNothingIsSaved(string field, string json)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateProfiledCommunityAsync(companyId, "Keystone Ridge");
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);
        var before = await ProfileRowAsync(community);

        var res = await manager.PatchAsync(Url(community), new StringContent(
            json.Replace("LONG", new string('a', 201)), System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        Assert.Equal([field], body.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()));
        Assert.Equal(before, await ProfileRowAsync(community));
        Assert.DoesNotContain(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CommunityProfileChanged"));
    }

    // US3-S18
    [Fact]
    public async Task RenamingToAnotherCommunitysName_IgnoringCaseAndSpaces_IsRefused()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateProfiledCommunityAsync(companyId, "Keystone Ridge");
        await CreateProfiledCommunityAsync(companyId, "Keystone Park");
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);

        var res = await manager.PatchAsJsonAsync(Url(community), new { communityName = " KEYSTONE PARK" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("COMMUNITY_NAME_TAKEN", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Equal("Keystone Ridge", (await ProfileRowAsync(community)).CommunityName);
    }

    // US3-S19
    [Fact]
    public async Task ABackfilledCommunity_SavesOtherFields_AndListsWhatIsStillMissing()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, name: "Keystone Ridge", legalName: "");
        var (_, managerEmail) = await CreateManagerAsync(community);
        await WithDbAsync(db => db.Communities.Where(c => c.Id == community).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.County, (string?)null)
            .SetProperty(c => c.FormationDate, new DateOnly(2004, 3, 14))
            .SetProperty(c => c.ManagementStartDate, new DateOnly(2020, 1, 1))));
        using var manager = await ClientForAsync(managerEmail);

        var res = await manager.PatchAsJsonAsync(Url(community), new { description = "A neighborhood of 120 homes." });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("A neighborhood of 120 homes.", body.GetProperty("description").GetString());
        Assert.Equal(["legalName", "county"], body.GetProperty("missingRequiredFields").EnumerateArray().Select(f => f.GetString()));
        Assert.Equal("A neighborhood of 120 homes.", (await ProfileRowAsync(community)).Description);
    }

    // US3-S20
    [Fact]
    public async Task AManager_CantActivateArchiveRestoreOrChangeTheParent()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateProfiledCommunityAsync(companyId, "Keystone Ridge");
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);

        var responses = new[]
        {
            await manager.PostAsync($"/api/v1/communities/{community}/activate", null),
            await manager.PostAsync($"/api/v1/communities/{community}/archive", null),
            await manager.PostAsync($"/api/v1/communities/{community}/restore", null),
            await manager.PutAsJsonAsync($"/api/v1/communities/{community}/parent", new { parentCommunityId = (Guid?)null }),
        };

        foreach (var res in responses)
        {
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        }
    }

    // US3-S21
    [Fact]
    public async Task AManagerOfAnotherCommunity_GetsTheSame403_AsForACommunityThatDoesntExist()
    {
        var companyId = await CreateCompanyAsync();
        var mine = await CreateProfiledCommunityAsync(companyId, "Keystone Ridge");
        var other = await CreateProfiledCommunityAsync(companyId, "Keystone Park");
        var (_, managerEmail) = await CreateManagerAsync(mine);
        using var manager = await ClientForAsync(managerEmail);
        var before = await ProfileRowAsync(other);

        var res = await manager.PatchAsJsonAsync(Url(other), new { county = "Wake" });
        var missing = await manager.PatchAsJsonAsync(Url(Guid.NewGuid()), new { county = "Wake" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Equal(missing.StatusCode, res.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await res.Content.ReadAsStringAsync());
        Assert.Equal(before, await ProfileRowAsync(other));
    }

    // Spec Edge Cases: a rename frees the old name.
    [Fact]
    public async Task ARename_FreesTheOldName()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateProfiledCommunityAsync(companyId, "Keystone Ridge");
        var (_, managerEmail) = await CreateManagerAsync(community);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var manager = await ClientForAsync(managerEmail);
        using var admin = await ClientForAsync(adminEmail);

        Assert.Equal(HttpStatusCode.OK,
            (await manager.PatchAsJsonAsync(Url(community), new { communityName = "Keystone Bluff" })).StatusCode);
        var created = await admin.PostAsJsonAsync("/api/v1/communities", new
        {
            legalName = "Keystone Ridge HOA, Inc.", communityName = "Keystone Ridge", county = "Hamilton",
            formationDate = "2004-03-14", managementStartDate = "2026-11-01",
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Keystone Ridge", (await JsonAsync(created)).GetProperty("communityName").GetString());
    }

    // US3-S17 (invalid values): a value of the wrong JSON type, or a date that isn't a real date.
    [Theory]
    [InlineData("legalName", "{\"legalName\":42}", "Enter text.")]
    [InlineData("description", "{\"description\":{\"text\":\"A neighborhood\"}}", "Enter text.")]
    [InlineData("formationDate", "{\"formationDate\":\"03/14/2004\"}", "Enter a real date as MM/DD/YYYY.")]
    [InlineData("formationDate", "{\"formationDate\":\"2004-02-30\"}", "Enter a real date as MM/DD/YYYY.")]
    [InlineData("managementStartDate", "{\"managementStartDate\":20200101}", "Enter a real date as MM/DD/YYYY.")]
    public async Task AWrongTypeOrAnUnreadableDate_IsRefusedWithItsMessage_AndNothingIsSaved(string field, string json, string message)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateProfiledCommunityAsync(companyId, "Keystone Ridge");
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);
        var before = await ProfileRowAsync(community);

        var res = await manager.PatchAsync(Url(community), new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal((field, message), (error.GetProperty("field").GetString(), error.GetProperty("message").GetString()));
        Assert.Equal(before, await ProfileRowAsync(community));
        Assert.DoesNotContain(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CommunityProfileChanged"));
    }

    // R15: a name another community takes between the check and the save is still refused, without naming it.
    [Fact]
    public async Task ANameTakenBetweenTheCheckAndTheSave_IsCommunityNameTaken()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateProfiledCommunityAsync(companyId, "Keystone Ridge");
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);
        _beforeRename.Once = () => CreateCommunityInAsync(companyId, name: "Keystone Bluff");

        var res = await manager.PatchAsJsonAsync(Url(community), new { communityName = "Keystone Bluff" });

        Assert.Null(_beforeRename.Once);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal(("COMMUNITY_NAME_TAKEN", "That community name is unavailable."),
            (body.GetProperty("code").GetString(), body.GetProperty("message").GetString()));
        Assert.Equal("Keystone Ridge", (await ProfileRowAsync(community)).CommunityName);
        Assert.Equal(1, await WithDbAsync(db => db.Communities.CountAsync(c => c.ManagementCompanyId == companyId
                                                                             && c.CommunityName == "Keystone Bluff")));
        Assert.DoesNotContain(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CommunityProfileChanged"));
    }

    // US3-S21 (reading the profile)
    [Fact]
    public async Task AManagerOfAnotherCommunity_CantReadItsProfile_AndGetsTheSame403_AsForACommunityThatDoesntExist()
    {
        var companyId = await CreateCompanyAsync();
        var mine = await CreateProfiledCommunityAsync(companyId, "Keystone Ridge");
        var other = await CreateProfiledCommunityAsync(companyId, "Keystone Park");
        var (_, managerEmail) = await CreateManagerAsync(mine);
        using var manager = await ClientForAsync(managerEmail);

        var res = await manager.GetAsync(Url(other));
        var missing = await manager.GetAsync(Url(Guid.NewGuid()));
        var own = await manager.GetAsync(Url(mine));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Equal(missing.StatusCode, res.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await res.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("Keystone Ridge", (await JsonAsync(own)).GetProperty("communityName").GetString());
    }

    private sealed record ProfileRow(string LegalName, string CommunityName, string? County, DateOnly? FormationDate,
        DateOnly? ManagementStartDate, string? Description);

    private static string Url(Guid community) => $"/api/v1/communities/{community}/profile";

    private async Task<Guid> CreateProfiledCommunityAsync(Guid companyId, string name)
    {
        var id = await CreateCommunityInAsync(companyId, name: name, legalName: $"{name} HOA, Inc.");
        await WithDbAsync(db => db.Communities.Where(c => c.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.County, "Hamilton")
            .SetProperty(c => c.FormationDate, new DateOnly(2004, 3, 14))
            .SetProperty(c => c.ManagementStartDate, new DateOnly(2020, 1, 1))
            .SetProperty(c => c.Description, (string?)null)));
        return id;
    }

    private Task<ProfileRow> ProfileRowAsync(Guid community) => WithDbAsync(db => db.Communities.AsNoTracking()
        .Where(c => c.Id == community)
        .Select(c => new ProfileRow(c.LegalName, c.CommunityName, c.County, c.FormationDate, c.ManagementStartDate, c.Description))
        .SingleAsync());

    /// <summary>Runs <see cref="Once"/> just before the save that renames a community, once armed.</summary>
    private sealed class BeforeRenamingACommunity : SaveChangesInterceptor
    {
        private Func<Task>? _once;

        public Func<Task>? Once { get => Volatile.Read(ref _once); set => Volatile.Write(ref _once, value); }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _once) is not null
                && eventData.Context!.ChangeTracker.Entries<Domain.Entities.Community>()
                    .Any(e => e.State == EntityState.Modified && e.Property(c => c.CommunityName).IsModified)
                && Interlocked.Exchange(ref _once, null) is { } run)
                await run();
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
