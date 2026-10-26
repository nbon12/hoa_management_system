using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T058 — the setup step for the first Company Administrator (FR-038; contracts/company.md §
/// Setup step). Each test runs on its own freshly migrated database, because the step reads the
/// deployment's single company. The command-line tests run the real entry point Program.cs calls,
/// <see cref="StartupTasks.RunGrantCompanyAdminCommandAsync(IServiceProvider, string[], TextWriter, TextWriter)"/>,
/// and read what it prints.
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
        // US7-S11: one event for the run, with the actor "setup", the target and the time.
        var logged = Assert.Single(SetupEvents());
        Assert.Equal(("Granted", userId, Clock.UtcNow), (Prop(logged, "Change"), Prop(logged, "TargetUserId"), At(logged)));
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
        // US7-S11: one event for the run, naming the invitation.
        var logged = Assert.Single(SetupEvents());
        Assert.Equal(("Invited", invitation.Id.ToString(), Clock.UtcNow),
            (Prop(logged, "Change"), Prop(logged, "InvitationId"), At(logged)));
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
        // US7-S11: each run is logged, including the two that changed nothing.
        Assert.Equal(["Granted", "NoChange", "NoChange"], SetupEvents().Select(e => Prop(e, "Change")));
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

    // FR-038: the setup step's invitation, accepted from the link, grants as the step's direct grant
    // does: Source = Setup, with no grantor.
    [Fact]
    public async Task TheSetupStepsInvitation_AcceptedFromTheLink_GrantsWithSourceSetup()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await SetupAsync(email);
        var invitationId = await WithDbAsync(db => db.Invitations.Where(i => i.Email == email).Select(i => i.Id).SingleAsync());
        var token = Regex.Match(Email.Sent.Single(m => m.Target == email).Body, "/invite#t=([A-Za-z0-9_-]{43})").Groups[1].Value;

        var res = await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token, firstName = "Robin", lastName = "Hale", password = "Juniper#2026x" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var grant = await WithDbAsync(db => db.CompanyAdministratorGrants.AsNoTracking().SingleAsync());
        Assert.Equal((CompanyGrantSource.Setup, (string?)null, (Guid?)invitationId, (DateTimeOffset?)null),
            (grant.Source, grant.GrantedByUserId, grant.InvitationId, grant.RevokedAt));
    }

    // US7-S6, from the command line, the email has an account: exits 0 and prints the grant.
    [Fact]
    public async Task FromTheCommandLine_ForAnAccount_ExitsWith0_AndPrintsTheGrant()
    {
        var userId = await WithDbAsync(CreateUserAsync);

        var result = await RunCommandLineAsync(["--grant-company-admin", EmailOf(userId)]);

        Assert.Equal(((int?)0, $"Granted Company Administrator to {userId}.{Environment.NewLine}", ""), result);
        var grant = await WithDbAsync(db => db.CompanyAdministratorGrants.AsNoTracking().SingleAsync());
        Assert.Equal((userId, CompanyGrantSource.Setup, (DateTimeOffset?)null), (grant.UserId, grant.Source, grant.RevokedAt));
        Assert.Empty(await WithDbAsync(db => db.Invitations.ToListAsync()));
    }

    // US7-S6, from the command line, the email has no account: exits 0 and prints the invitation.
    [Fact]
    public async Task FromTheCommandLine_ForNoAccount_ExitsWith0_AndPrintsTheInvitation()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        Clock.UtcNow = new DateTimeOffset(2026, 10, 12, 11, 5, 0, TimeSpan.Zero);

        var result = await RunCommandLineAsync(["--grant-company-admin", email]);

        Assert.Equal(((int?)0, $"Invitation sent; it expires 2026-10-15 11:05 UTC.{Environment.NewLine}", ""), result);
        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync());
        Assert.Equal((InvitationKind.CompanyAdministrator, InvitationState.Pending, email),
            (invitation.Kind, invitation.State, invitation.Email));
        Assert.Equal(email, Assert.Single(Email.Sent).Target);
        Assert.Empty(await WithDbAsync(db => db.CompanyAdministratorGrants.ToListAsync()));
    }

    // US7-S7, from the command line: a second run exits 0, prints the no-change line and writes nothing.
    [Fact]
    public async Task FromTheCommandLine_ASecondRun_ExitsWith0_PrintsNothingChanged_AndWritesNothing()
    {
        var userId = await WithDbAsync(CreateUserAsync);
        Assert.Equal((int?)0, (await RunCommandLineAsync(["--grant-company-admin", EmailOf(userId)])).ExitCode);
        var before = await CountsAsync();

        var again = await RunCommandLineAsync(["--grant-company-admin", EmailOf(userId)]);
        var other = await RunCommandLineAsync(["--grant-company-admin", $"someone-{Guid.NewGuid():N}@example.com"]);

        Assert.Equal(((int?)0, $"A Company Administrator already exists; nothing changed.{Environment.NewLine}", ""), again);
        Assert.Equal(again, other);
        Assert.Equal(before, await CountsAsync());
        Assert.Empty(Email.Sent);
    }

    // FR-038, from the command line: an unusable email exits 2 with the reason on stderr and writes nothing.
    [Theory]
    [InlineData(new[] { "--grant-company-admin", "not-an-email" }, "\"not-an-email\" isn't a valid email address.")]
    [InlineData(new[] { "--grant-company-admin", "owner@localhost" }, "\"owner@localhost\" isn't a valid email address.")]
    [InlineData(new[] { "--grant-company-admin" },
        "--grant-company-admin needs an email address, for example: --grant-company-admin owner@example.com")]
    public async Task FromTheCommandLine_AnInvalidEmail_ExitsWith2_WithTheReasonOnStderr(string[] args, string reason)
    {
        var before = await CountsAsync();

        var result = await RunCommandLineAsync(args);

        Assert.Equal(((int?)2, "", reason + Environment.NewLine), result);
        Assert.Equal(before, await CountsAsync());
        Assert.Empty(Email.Sent);
    }

    // FR-038: without the verb the entry point returns null, so normal startup continues; it prints
    // and writes nothing.
    [Fact]
    public async Task FromTheCommandLine_WithoutTheVerb_ReturnsNull_AndDoesNothing()
    {
        var before = await CountsAsync();

        var result = await RunCommandLineAsync([]);

        Assert.Equal(((int?)null, "", ""), result);
        Assert.Equal(before, await CountsAsync());
    }

    private List<Serilog.Events.LogEvent> SetupEvents() => LogSink.Events
        .Where(e => e.MessageTemplate.Text.StartsWith("CompanyAdminChange") && Prop(e, "ActorId") == "setup").ToList();

    private static string? Prop(Serilog.Events.LogEvent e, string name) =>
        e.Properties.TryGetValue(name, out var v) ? v.ToString().Trim('"') : null;

    private static DateTimeOffset At(Serilog.Events.LogEvent e) =>
        (DateTimeOffset)((Serilog.Events.ScalarValue)e.Properties["UtcNow"]).Value!;

    private async Task<SetupResult> SetupAsync(string email, bool recover = false, string? companyName = null)
    {
        using var scope = NewScope();
        return await scope.ServiceProvider.GetRequiredService<CompanyAdministratorService>()
            .SetupAsync(email, recover, companyName, default);
    }

    /// <summary>Runs the setup step's command-line entry point on this test's host and captures what it prints.</summary>
    private async Task<(int? ExitCode, string Stdout, string Stderr)> RunCommandLineAsync(string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await StartupTasks.RunGrantCompanyAdminCommandAsync(Services, args, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    private Task<(int Grants, int Invitations, int Outbox)> CountsAsync() => WithDbAsync(async db => (
        await db.CompanyAdministratorGrants.CountAsync(),
        await db.Invitations.CountAsync(),
        await db.OutboxMessages.CountAsync()));
}
