using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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
        // US7-S11: exactly one event for the run, with its change, target and UTC time.
        var logged = Assert.Single(SetupEvents());
        Assert.Equal(("\"Recovery\"", $"\"{ownerId}\"", Clock.UtcNow),
            (logged.Properties["Change"].ToString(), logged.Properties["TargetUserId"].ToString(),
             (DateTimeOffset)((Serilog.Events.ScalarValue)logged.Properties["UtcNow"]).Value!));
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
        // US7-S11: exactly one event for the run, with its change, invitation and UTC time.
        var logged = Assert.Single(SetupEvents());
        Assert.Equal(("\"Recovery\"", invitation.Id.ToString(), Clock.UtcNow),
            (logged.Properties["Change"].ToString(), logged.Properties["InvitationId"].ToString(),
             (DateTimeOffset)((Serilog.Events.ScalarValue)logged.Properties["UtcNow"]).Value!));
        var row = Assert.Single(await RecoveryRowsAsync());
        Assert.Equal((adminId, $"co:recovery:{invitation.Id}:{adminId}"), (row.RecipientUserId, row.DedupKey));
        Assert.Equal(1, await WithDbAsync(db => db.CompanyAdministratorGrants.CountAsync()));
    }

    // US7-S8: the recovery invitation, accepted from the link, grants with Source = Recovery, as the
    // step's direct recovery grant does, and the existing administrator keeps the role.
    [Fact]
    public async Task ARecoveryInvitation_AcceptedFromTheLink_GrantsWithSourceRecovery()
    {
        var companyId = await WithDbAsync(db => DeploymentCompany.IdAsync(db));
        var (adminId, _) = await CreateCompanyAdminAsync(companyId);
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await SetupAsync(email, recover: true);
        var invitationId = await WithDbAsync(db => db.Invitations.Where(i => i.Email == email).Select(i => i.Id).SingleAsync());
        var token = Regex.Match(Email.Sent.Single(m => m.Target == email).Body, "/invite#t=([A-Za-z0-9_-]{43})").Groups[1].Value;

        var res = await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token, firstName = "Robin", lastName = "Hale", password = "Juniper#2026x" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var grant = await WithDbAsync(db => db.CompanyAdministratorGrants.AsNoTracking().SingleAsync(g => g.UserId != adminId));
        Assert.Equal((CompanyGrantSource.Recovery, (string?)null, (Guid?)invitationId, (DateTimeOffset?)null),
            (grant.Source, grant.GrantedByUserId, grant.InvitationId, grant.RevokedAt));
        Assert.Equal(2, await WithDbAsync(db => db.CompanyAdministratorGrants.CountAsync(g => g.RevokedAt == null)));
        // The administrator was told when the invitation was sent, and isn't told again.
        var row = Assert.Single(await RecoveryRowsAsync());
        Assert.Equal((adminId, $"co:recovery:{invitationId}:{adminId}"), (row.RecipientUserId, row.DedupKey));
    }

    // FR-038: a setup invitation sent while there was no administrator is accepted after another
    // run granted someone directly. The grant is a recovery grant, and is never silent.
    [Fact]
    public async Task ASetupInvitation_AcceptedAfterAnotherGrant_IsARecoveryGrant_AndTheAdministratorIsTold()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await SetupAsync(email, recover: false);
        var invitationId = await WithDbAsync(db => db.Invitations.Where(i => i.Email == email).Select(i => i.Id).SingleAsync());
        var firstId = await WithDbAsync(CreateUserAsync);
        Assert.Equal(new SetupResult(0, $"Granted Company Administrator to {firstId}."), await SetupAsync(EmailOf(firstId), recover: false));
        Assert.Empty(await RecoveryRowsAsync());
        var token = Regex.Match(Email.Sent.Single(m => m.Target == email).Body, "/invite#t=([A-Za-z0-9_-]{43})").Groups[1].Value;

        var res = await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token, firstName = "Robin", lastName = "Hale", password = "Juniper#2026x" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var grant = await WithDbAsync(db => db.CompanyAdministratorGrants.AsNoTracking().SingleAsync(g => g.InvitationId == invitationId));
        Assert.Equal((CompanyGrantSource.Recovery, (string?)null), (grant.Source, grant.GrantedByUserId));
        var row = Assert.Single(await RecoveryRowsAsync());
        Assert.Equal((firstId, $"co:recovery:{invitationId}:{firstId}"), (row.RecipientUserId, row.DedupKey));
        Assert.Single(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("CompanyAdminChange")
            && e.Properties["Change"].ToString() == "\"Recovery\""
            && e.Properties["InvitationId"].ToString() == invitationId.ToString()
            && e.Properties["TargetUserId"].ToString() == $"\"{grant.UserId}\"");
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
        Assert.Equal("\"NoChange\"", Assert.Single(SetupEvents()).Properties["Change"].ToString());
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

    private List<Serilog.Events.LogEvent> SetupEvents() => LogSink.Events
        .Where(e => e.MessageTemplate.Text.StartsWith("CompanyAdminChange") && e.Properties["ActorId"].ToString() == "\"setup\"")
        .ToList();

    private Task<List<OutboxMessage>> RecoveryRowsAsync() => WithDbAsync(db =>
        db.OutboxMessages.AsNoTracking().Where(m => m.Kind == CompanyEmailKinds.AdminRecovery).ToListAsync());
}
