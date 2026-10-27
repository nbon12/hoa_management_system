using System.Net.Mail;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Company;

/// <summary>What the setup step prints and its exit code: 0 done, 1 failed, 2 bad arguments.</summary>
public sealed record SetupResult(int ExitCode, string Output);

/// <summary>
/// The parsed <c>--grant-company-admin &lt;email&gt; [--recover] [--company-name "&lt;name&gt;"]</c>
/// verb (030 FR-038; contracts/company.md § Setup step). Never an HTTP route.
/// </summary>
public sealed record CompanyAdminSetupCommand(string Email, bool Recover, string? CompanyName)
{
    public const string Verb = "--grant-company-admin";

    /// <summary>
    /// Null when the verb is absent. Otherwise the command, or (in <paramref name="error"/>) why
    /// the arguments can't be used.
    /// </summary>
    public static CompanyAdminSetupCommand? TryParse(string[] args, out string? error)
    {
        error = null;
        var at = Array.IndexOf(args, Verb);
        if (at < 0)
            return null;

        var email = at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal) ? args[at + 1] : null;
        if (email is null)
        {
            error = $"{Verb} needs an email address, for example: {Verb} owner@example.com";
            return null;
        }

        string? companyName = null;
        var nameAt = Array.IndexOf(args, "--company-name");
        if (nameAt >= 0)
        {
            companyName = nameAt + 1 < args.Length ? args[nameAt + 1] : null;
            if (companyName is null || companyName.StartsWith("--", StringComparison.Ordinal))
            {
                error = "--company-name needs a name, for example: --company-name \"Juniper Row Community Management\"";
                return null;
            }
        }
        return new CompanyAdminSetupCommand(email, args.Contains("--recover"), companyName);
    }
}

/// <summary>
/// The operator's setup step for the first Company Administrator (030 FR-038; research R18). It
/// reads the deployment's single company. With no live grant, it grants the role to an existing
/// account or emails a Company Administrator invitation from the setup step; once a grant exists
/// it changes nothing, unless <c>--recover</c> is given: then it grants or invites anyway
/// (<c>Source = Recovery</c>) and tells every current Company Administrator.
/// </summary>
public sealed class CompanyAdministratorService(
    ApplicationDbContext db,
    InvitationService invitations,
    CompanyEmailRenderer emails,
    TimeProvider clock,
    ILogger<CompanyAdministratorService> logger)
{
    private const string SetupActor = "setup";
    private const int MaxCompanyNameLength = 200;

    private enum SetupStep
    {
        AlreadyExists,
        AlreadyAdministrator,
        Granted,
        NeedsInvitation
    }

    public async Task<SetupResult> SetupAsync(string email, bool recover, string? companyName, CancellationToken ct)
    {
        if (!IsEmail(email))
            return new SetupResult(2, $"\"{email}\" isn't a valid email address.");
        var name = companyName?.Trim();
        if (name is { Length: 0 or > MaxCompanyNameLength })
            return new SetupResult(2, $"--company-name must be 1 to {MaxCompanyNameLength} characters.");
        var companyId = await DeploymentCompany.IdAsync(db, ct);
        var normalized = Invitation.Normalize(email).ToUpperInvariant();
        string? grantedUserId = null;
        List<Administrator> current = [];
        string? alreadyUserId = null;
        var renamed = false;

        var step = await CompanyLocks.RunAsync(db, companyId, async () =>
        {
            if (name is not null)
            {
                var company = await db.ManagementCompanies.FirstAsync(c => c.Id == companyId, ct);
                renamed = company.Name != name;
                company.Name = name;
                await db.SaveChangesAsync(ct);
            }

            current = await db.CompanyAdministratorGrants
                .Where(g => g.CompanyId == companyId && g.RevokedAt == null)
                .Select(g => new Administrator(g.UserId, g.User.Email))
                .ToListAsync(ct);
            if (current.Count > 0 && !recover)
                return SetupStep.AlreadyExists;

            var userId = await db.Users.Where(u => u.NormalizedEmail == normalized).Select(u => u.Id).FirstOrDefaultAsync(ct);
            if (userId is null)
                return SetupStep.NeedsInvitation;
            if (current.Any(a => a.UserId == userId))
            {
                alreadyUserId = userId;
                return SetupStep.AlreadyAdministrator;
            }

            var now = clock.GetUtcNow();
            var recovering = current.Count > 0;
            var grant = new CompanyAdministratorGrant
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = userId,
                Source = recovering ? CompanyGrantSource.Recovery : CompanyGrantSource.Setup,
                GrantedAt = now,
            };
            db.CompanyAdministratorGrants.Add(grant);
            if (recovering)
                await NotifyRecoveryAsync(current, grant.Id, email, now, ct);
            await db.SaveChangesAsync(ct);
            CompanyLog.CompanyAdminChange(logger, SetupActor, companyId, userId, null, recovering ? "Recovery" : "Granted", now);
            grantedUserId = userId;
            return SetupStep.Granted;
        }, ct);

        if (renamed)
            logger.LogInformation("CompanyRenamed: Actor={ActorId} Company={CompanyId} At={UtcNow:o}", SetupActor, companyId,
                clock.GetUtcNow());
        // FR-039: every run is a sensitive event, including one that changes nothing.
        if (step is SetupStep.AlreadyExists or SetupStep.AlreadyAdministrator)
            CompanyLog.CompanyAdminChange(logger, SetupActor, companyId, alreadyUserId, null, "NoChange", clock.GetUtcNow());

        return step switch
        {
            SetupStep.AlreadyExists => new SetupResult(0, "A Company Administrator already exists; nothing changed."),
            SetupStep.AlreadyAdministrator => new SetupResult(0, $"{email} is already a Company Administrator; nothing changed."),
            SetupStep.Granted => new SetupResult(0, $"Granted Company Administrator to {grantedUserId}."),
            _ => await InviteAsync(companyId, email, current, ct),
        };
    }

    private sealed record Administrator(string UserId, string? Email);

    // FR-038: a recovery grant or invitation is never silent; every current administrator hears of it,
    // once per grant or invitation, so running the step again to resend a failed email adds nothing.
    private async Task NotifyRecoveryAsync(IEnumerable<Administrator> current, Guid grantOrInvitationId, string email,
        DateTimeOffset now, CancellationToken ct)
    {
        foreach (var admin in current.Where(a => !string.IsNullOrEmpty(a.Email)))
        {
            var row = emails.AdminRecovery(admin.UserId, admin.Email!, grantOrInvitationId, email, now);
            if (!await db.OutboxMessages.AnyAsync(m => m.DedupKey == row.DedupKey, ct))
                db.OutboxMessages.Add(row);
        }
    }

    private async Task<SetupResult> InviteAsync(Guid companyId, string email, IReadOnlyList<Administrator> current, CancellationToken ct)
    {
        Invitation invitation;
        try
        {
            try
            {
                invitation = await invitations.CreateAsync(
                    new NewInvitation(InvitationKind.CompanyAdministrator, companyId, null, null, null, null, email), null, ct);
            }
            catch (DomainException ex) when (ex.Code == "INVITATION_PENDING")
            {
                // A run whose email failed to send, run again: send the same invitation a new link.
                invitation = await invitations.ResendAsync(null, (Guid)ex.Extensions!["invitationId"]!, ct);
            }
        }
        catch (DomainException ex) when (ex.Code == "RATE_LIMITED")
        {
            CompanyLog.CompanyAdminChange(logger, SetupActor, companyId, null, null, "RateLimited", clock.GetUtcNow());
            return new SetupResult(1, "Too many invitation emails were sent to this address recently. Wait a few minutes, then run this step again.");
        }

        var now = clock.GetUtcNow();
        if (current.Count > 0)
        {
            await NotifyRecoveryAsync(current, invitation.Id, email, now, ct);
            await db.SaveChangesAsync(ct);
        }
        CompanyLog.CompanyAdminChange(logger, SetupActor, companyId, null, invitation.Id, current.Count > 0 ? "Recovery" : "Invited", now);

        return invitation.LastSendError is null
            ? new SetupResult(0, $"Invitation sent; it expires {invitation.ExpiresAt!.Value.UtcDateTime:yyyy-MM-dd HH:mm} UTC.")
            : new SetupResult(1, "The invitation was created, but its email couldn't be sent. Check the email configuration, then run this step again.");
    }

    private static bool IsEmail(string email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Trim().Length <= 256
        && MailAddress.TryCreate(email.Trim(), out var parsed)
        && parsed.Address == email.Trim()
        && parsed.Host.Contains('.');
}
