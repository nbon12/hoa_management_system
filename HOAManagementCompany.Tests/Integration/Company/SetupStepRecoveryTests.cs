using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T135 — US7-S8: the setup step's recovery mode, for when every Company Administrator is
/// unreachable (FR-038; contracts/company.md § Setup step). Each test runs on its own database,
/// because the step reads the deployment's single company.
/// </summary>
public class SetupStepRecoveryTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private string? _database;

    protected override string DatabaseConnectionString() =>
        _database ??= Task.Run(() => Fixture.CreateFreshDatabaseAsync()).GetAwaiter().GetResult();

    // US7-S8, the email has an account.
    [Fact]
    public async Task WithAnAdministratorPresent_RecoverGrantsTheRole_AndTellsEveryCurrentAdministrator()
    {
        var companyId = await WithDbAsync(db => DeploymentCompany.IdAsync(db));
        var (firstId, _) = await CreateCompanyAdminAsync(companyId);
        var (secondId, _) = await CreateCompanyAdminAsync(companyId);
        var ownerId = await WithDbAsync(CreateUserAsync);

        var result = await SetupAsync(EmailOf(ownerId), recover: true);

        Assert.Equal(new SetupResult(0, $"Granted Company Administrator to {ownerId}."), result);
        var grant = await WithDbAsync(db => db.CompanyAdministratorGrants.AsNoTracking().SingleAsync(g => g.UserId == ownerId));
        Assert.Equal((CompanyGrantSource.Recovery, (DateTimeOffset?)null), (grant.Source, grant.RevokedAt));
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CompanyAdminChange")
            && e.Properties["ActorId"].ToString() == "\"setup\""
            && (DateTimeOffset)((Serilog.Events.ScalarValue)e.Properties["UtcNow"]).Value! == Clock.UtcNow
            && e.Properties["Change"].ToString() == "\"Recovery\""
            && e.Properties["TargetUserId"].ToString() == $"\"{ownerId}\"");
        var rows = await RecoveryRowsAsync();
        Assert.Equal(new[] { firstId, secondId }.Order(), rows.Select(r => r.RecipientUserId!).Order());
        Assert.All(rows, r => Assert.Equal($"co:recovery:{grant.Id}:{r.RecipientUserId}", r.DedupKey));
    }

    // US7-S8, no account yet.
    [Fact]
    public async Task WithAnAdministratorPresent_RecoverInvitesAnEmailWithNoAccount()
    {
        var companyId = await WithDbAsync(db => DeploymentCompany.IdAsync(db));
        var (adminId, _) = await CreateCompanyAdminAsync(companyId);
        var email = $"owner-{Guid.NewGuid():N}@example.com";

        var result = await SetupAsync(email, recover: true);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("Invitation sent; it expires", result.Output);
        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Email == email));
        Assert.Equal((InvitationKind.CompanyAdministrator, InvitationState.Pending, (string?)null),
            (invitation.Kind, invitation.State, invitation.SenderUserId));
        Assert.Single(Email.Sent, m => m.Target == email);
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CompanyAdminChange")
            && e.Properties["ActorId"].ToString() == "\"setup\""
            && (DateTimeOffset)((Serilog.Events.ScalarValue)e.Properties["UtcNow"]).Value! == Clock.UtcNow
            && e.Properties["Change"].ToString() == "\"Recovery\""
            && e.Properties["InvitationId"].ToString() == invitation.Id.ToString());
        var row = Assert.Single(await RecoveryRowsAsync());
        Assert.Equal((adminId, $"co:recovery:{invitation.Id}:{adminId}"), (row.RecipientUserId, row.DedupKey));
        Assert.Equal(1, await WithDbAsync(db => db.CompanyAdministratorGrants.CountAsync()));
    }

    [Fact]
    public async Task WithoutRecover_AnExistingAdministratorMeansNothingChanges()
    {
        var companyId = await WithDbAsync(db => DeploymentCompany.IdAsync(db));
        await CreateCompanyAdminAsync(companyId);
        var ownerId = await WithDbAsync(CreateUserAsync);

        var result = await SetupAsync(EmailOf(ownerId), recover: false);

        Assert.Equal(new SetupResult(0, "A Company Administrator already exists; nothing changed."), result);
        Assert.False(await WithDbAsync(db => db.CompanyAdministratorGrants.AnyAsync(g => g.UserId == ownerId)));
        Assert.Empty(await RecoveryRowsAsync());
        // FR-039: a run that changes nothing is still a sensitive event.
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CompanyAdminChange")
            && e.Properties["ActorId"].ToString() == "\"setup\"" && e.Properties["Change"].ToString() == "\"NoChange\"");
    }

    // A run whose email failed is run again: the same invitation gets a new link, and the
    // administrators aren't told twice.
    [Fact]
    public async Task RunningRecoverAgain_ResendsTheSameInvitation_WithoutNotifyingTwice()
    {
        var companyId = await WithDbAsync(db => DeploymentCompany.IdAsync(db));
        var (adminId, _) = await CreateCompanyAdminAsync(companyId);
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        Assert.Equal(0, (await SetupAsync(email, recover: true)).ExitCode);
        Clock.UtcNow = Clock.UtcNow.AddMinutes(2);

        var again = await SetupAsync(email, recover: true);

        Assert.Equal(0, again.ExitCode);
        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Email == email));
        Assert.Equal((InvitationState.Pending, 1), (invitation.State, invitation.ResendCount));
        Assert.Equal(2, Email.Sent.Count(m => m.Target == email));
        Assert.Equal(adminId, Assert.Single(await RecoveryRowsAsync()).RecipientUserId);
        Assert.Equal(2, LogSink.Events.Count(e => e.MessageTemplate.Text.StartsWith("CompanyAdminChange")
            && e.Properties["ActorId"].ToString() == "\"setup\""
            && e.Properties["Change"].ToString() == "\"Recovery\""));
    }

    // Within the resend cooldown, the run explains itself instead of failing.
    [Fact]
    public async Task RunningRecoverAgainTooSoon_ExitsWith1_AndSaysToWait()
    {
        var companyId = await WithDbAsync(db => DeploymentCompany.IdAsync(db));
        await CreateCompanyAdminAsync(companyId);
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        Assert.Equal(0, (await SetupAsync(email, recover: true)).ExitCode);

        var again = await SetupAsync(email, recover: true);

        Assert.Equal(1, again.ExitCode);
        Assert.Contains("Wait a few minutes", again.Output);
        Assert.Single(Email.Sent, m => m.Target == email);
    }

    private async Task<SetupResult> SetupAsync(string email, bool recover, string? companyName = null)
    {
        using var scope = NewScope();
        return await scope.ServiceProvider.GetRequiredService<CompanyAdministratorService>()
            .SetupAsync(email, recover, companyName, default);
    }

    private Task<List<OutboxMessage>> RecoveryRowsAsync() => WithDbAsync(db =>
        db.OutboxMessages.AsNoTracking().Where(m => m.Kind == CompanyEmailKinds.AdminRecovery).ToListAsync());
}
