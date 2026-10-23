using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T058 — the setup step for the first Company Administrator (FR-038; contracts/company.md §
/// Setup step). Each test runs on its own freshly migrated database, because the step reads the
/// deployment's single company.
/// </summary>
public class SetupStepTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private string? _database;

    protected override string DatabaseConnectionString() =>
        _database ??= Task.Run(() => Fixture.CreateFreshDatabaseAsync()).GetAwaiter().GetResult();

    // US7-S6, the email has an account.
    [Fact]
    public async Task NoAdministrator_AndAnAccount_GrantsTheRole()
    {
        var userId = await WithDbAsync(CreateUserAsync);

        var result = await SetupAsync(EmailOf(userId));

        Assert.Equal(new SetupResult(0, $"Granted Company Administrator to {userId}."), result);
        var grant = await WithDbAsync(db => db.CompanyAdministratorGrants.AsNoTracking().SingleAsync());
        Assert.Equal((userId, CompanyGrantSource.Setup, (string?)null, (DateTimeOffset?)null),
            (grant.UserId, grant.Source, grant.GrantedByUserId, grant.RevokedAt));
        Assert.Equal(await WithDbAsync(db => DeploymentCompany.IdAsync(db)), grant.CompanyId);
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CompanyAdminChange")
            && e.Properties["ActorId"].ToString() == "\"setup\""
            && e.Properties["Change"].ToString() == "\"Granted\""
            && e.Properties["TargetUserId"].ToString() == $"\"{userId}\"");
        Assert.Empty(Email.Sent);
    }

    // US7-S6, the email has no account.
    [Fact]
    public async Task NoAdministrator_AndNoAccount_SendsAnInvitationFromTheSetupStep()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        Clock.UtcNow = new DateTimeOffset(2026, 10, 12, 11, 5, 0, TimeSpan.Zero);

        var result = await SetupAsync(email);

        Assert.Equal(new SetupResult(0, "Invitation sent; it expires 2026-10-15 11:05 UTC."), result);
        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync());
        Assert.Equal((InvitationKind.CompanyAdministrator, InvitationState.Pending, (string?)null, email),
            (invitation.Kind, invitation.State, invitation.SenderUserId, invitation.Email));
        var sent = Assert.Single(Email.Sent);
        Assert.Equal(email, sent.Target);
        Assert.Contains("Sent by: Setup step", sent.Body);
        Assert.Empty(await WithDbAsync(db => db.CompanyAdministratorGrants.ToListAsync()));
    }

    // US7-S7: once an administrator exists, the step changes nothing.
    [Fact]
    public async Task ASecondRun_ChangesNothing()
    {
        var userId = await WithDbAsync(CreateUserAsync);
        await SetupAsync(EmailOf(userId));
        var before = await CountsAsync();

        var again = await SetupAsync(EmailOf(userId));
        var other = await SetupAsync($"someone-{Guid.NewGuid():N}@example.com");

        Assert.Equal(new SetupResult(0, "A Company Administrator already exists; nothing changed."), again);
        Assert.Equal(again, other);
        Assert.Equal(before, await CountsAsync());
        Assert.Empty(Email.Sent);
    }

    [Fact]
    public async Task CompanyName_SetsTheCompanysName()
    {
        var userId = await WithDbAsync(CreateUserAsync);

        var result = await SetupAsync(EmailOf(userId), companyName: "  Juniper Row Community Management ");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Juniper Row Community Management",
            await WithDbAsync(db => db.ManagementCompanies.Select(c => c.Name).SingleAsync()));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("owner@localhost")]
    [InlineData("")]
    [InlineData("Owner <owner@example.com>")]
    public async Task AnInvalidEmail_ExitsWith2_AndWritesNothing(string email)
    {
        var before = await CountsAsync();

        var result = await SetupAsync(email);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("isn't a valid email address", result.Output);
        Assert.Equal(before, await CountsAsync());
    }

    private async Task<SetupResult> SetupAsync(string email, bool recover = false, string? companyName = null)
    {
        using var scope = NewScope();
        return await scope.ServiceProvider.GetRequiredService<CompanyAdministratorService>()
            .SetupAsync(email, recover, companyName, default);
    }

    private Task<(int Grants, int Invitations, int Outbox)> CountsAsync() => WithDbAsync(async db => (
        await db.CompanyAdministratorGrants.CountAsync(),
        await db.Invitations.CountAsync(),
        await db.OutboxMessages.CountAsync()));
}
