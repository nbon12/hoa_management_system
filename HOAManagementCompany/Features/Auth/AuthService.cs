using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth.Models;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HOAManagementCompany.Features.Auth;

// <!-- REPOWISE:START domain=auth -->
// Auth service: register, login, logout, token refresh, switch-property, get-me
// <!-- REPOWISE:END -->

public class AuthService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    IConfiguration config,
    ILogger<AuthService> logger,
    EmailVerificationService emailVerification,
    ClaimCodeService claimCodes)
{
    private int AccessTokenExpiryMinutes => config.GetValue("Jwt:AccessTokenExpiryMinutes", 15);
    private int RefreshTokenExpiryDays => config.GetValue("Jwt:RefreshTokenExpiryDays", 30);
    private string JwtSecret => config["Jwt:Secret"]!;
    private string JwtIssuer => config["Jwt:Issuer"]!;
    private string JwtAudience => config["Jwt:Audience"]!;

    // 016-A FR-A1/A3/A5: bind a user to a property only after (1) an email-verification proof and
    // (2) a valid single-use claim code. All failures are generic to avoid an enumeration oracle.
    private const string RegistrationFailed = "REGISTRATION_FAILED";

    public async Task<AuthResult> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        var verification = await emailVerification.ResolveProofAsync(
                req.VerificationToken, EmailVerificationService.PurposeRegistration, ct)
            ?? throw new DomainException(RegistrationFailed, "Registration could not be completed.", 422);
        var email = verification.Email;

        var claimCode = await claimCodes.FindActiveAsync(req.ClaimCode, ct)
            ?? throw new DomainException(RegistrationFailed, "Registration could not be completed.", 422);

        if (await userManager.FindByEmailAsync(email) is not null
            || await db.UserProperties.AnyAsync(up => up.PropertyId == claimCode.PropertyId, ct))
            throw new DomainException(RegistrationFailed, "Registration could not be completed.", 422);

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            FirstName = req.FirstName,
            LastName = req.LastName,
            EmailConfirmed = true,
            // 016-A FR-A4: enable per-account lockout for new users.
            LockoutEnabled = true
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            throw new DomainException("VALIDATION_ERROR", string.Join("; ", result.Errors.Select(e => e.Description)), 422);

        db.UserProperties.Add(new UserProperty { UserId = user.Id, PropertyId = claimCode.PropertyId });
        claimCode.RedeemedAt = DateTimeOffset.UtcNow;
        claimCode.RedeemedByUserId = user.Id;
        verification.ConsumedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var property = await db.Properties.FirstAsync(p => p.Id == claimCode.PropertyId, ct);
        logger.LogInformation("User registered and claimed property {PropertyId}", claimCode.PropertyId);
        return await CreateTokenPairAsync(user, property, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        // 016-A FR-A4: lockout-aware login. Responses stay generic so lock state is not an oracle.
        if (user is null)
        {
            logger.LogWarning("Failed login attempt for {Email}", req.Email);
            throw new DomainException("INVALID_CREDENTIALS", "Invalid email or password.", 401);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login attempt on locked-out account {Email}", req.Email);
            throw new DomainException("INVALID_CREDENTIALS", "Invalid email or password.", 401);
        }

        if (!await userManager.CheckPasswordAsync(user, req.Password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Failed login attempt for {Email}", req.Email);
            throw new DomainException("INVALID_CREDENTIALS", "Invalid email or password.", 401);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var property = await FindUsablePropertyAsync(user.Id, ct);
        logger.LogInformation("User logged in: {Email}", req.Email);
        return await CreateTokenPairAsync(user, property, ct);
    }

    public async Task LogoutAsync(string userId, CancellationToken ct = default)
    {
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);
        logger.LogInformation("User logged out: {UserId}", userId);
    }

    public async Task<AuthResult> RefreshAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = HashToken(rawToken);

        // 030 R11: the old token's deletion and the new pair's mint commit together, so a failure
        // between them can't leave the user without a usable refresh token.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var stored = await db.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

            if (stored is null || !stored.IsActive)
                throw new DomainException("INVALID_REFRESH_TOKEN", "The refresh token is invalid or has expired.", 401);

            db.RefreshTokens.Remove(stored);
            var property = await FindUsablePropertyAsync(stored.UserId, ct);
            var result = await CreateTokenPairAsync(stored.User, property, ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    public async Task<AuthResult> SwitchPropertyAsync(string userId, Guid propertyId, CancellationToken ct = default)
    {
        var link = await db.UserProperties
            .Include(up => up.Property)
            .FirstOrDefaultAsync(up => up.UserId == userId && up.PropertyId == propertyId, ct);

        if (link is null)
            throw new DomainException("PROPERTY_ACCESS_DENIED", "You are not linked to the requested property.", 403);

        // 030 FR-012a: an Onboarding home isn't live yet; an Archived one stays selectable read-only.
        var status = await db.Communities
            .Where(c => c.Id == link.Property.CommunityId)
            .Select(c => c.Status)
            .FirstAsync(ct);
        if (status == CommunityStatus.Onboarding)
            throw new DomainException("COMMUNITY_NOT_ACTIVE",
                "This community isn't live yet. Your community manager will let you know when it is.", 409);

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new DomainException("NOT_FOUND", "User not found.", 404);

        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);

        return await CreateTokenPairAsync(user, link.Property, ct);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new DomainException("NOT_FOUND", "User not found.", 404);

        return await BuildCurrentUserAsync(user, ct);
    }

    // 025 FR-018/FR-020/FR-022/FR-023: switch the caller's active UI mode. Board mode
    // requires an active non-resident membership, verified server-side (never from the
    // client's requested mode — FR-014). Persists LastActiveMode and rotates refresh
    // tokens, mirroring SwitchPropertyAsync. 030 FR-036b: memberships in Onboarding
    // communities count, and a user with no home can switch.
    public async Task<AuthResult> SwitchModeAsync(string userId, UserMode mode, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException("NOT_FOUND", "User not found.", 404);

        if (mode == UserMode.Board && !await HasActiveBoardMembershipAsync(userId, ct))
            throw new DomainException("NO_ACTIVE_MEMBERSHIP", "You do not hold an active board membership.", 403);

        user.LastActiveMode = mode;

        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);

        var property = await FindUsablePropertyAsync(userId, ct);
        return await CreateTokenPairAsync(user, property, ct);
    }

    private Task<bool> HasActiveBoardMembershipAsync(string userId, CancellationToken ct) =>
        EffectiveMemberships(userId).AnyAsync(m => m.Role != CommunityRole.Resident, ct);

    // 030 FR-036b: effective memberships in Onboarding and Active communities; Archived ones are left out.
    private IQueryable<CommunityMembership> EffectiveMemberships(string userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return db.CommunityMemberships.Where(m =>
            m.UserId == userId
            && m.Status == MembershipStatus.Active
            && (m.EndDate == null || m.EndDate >= today)
            && m.Community.Status != CommunityStatus.Archived);
    }

    private async Task<CurrentUserDto> BuildCurrentUserAsync(ApplicationUser user, CancellationToken ct)
    {
        // 030 R11: homes in Active and Archived communities, in the order FindUsablePropertyAsync
        // picks from; Onboarding homes aren't live yet and are left out.
        var links = await db.UserProperties
            .Where(up => up.UserId == user.Id)
            .OrderBy(up => up.Property.Community.Status == CommunityStatus.Active ? 0 : 1)
            .ThenBy(up => up.LinkedAt)
            .Select(up => new
            {
                up.Property.Community.Status,
                Summary = new PropertySummaryDto(up.Property.Id, up.Property.AccountNumber, up.Property.Address,
                    up.Property.CommunityId, up.Property.Community.CommunityName, up.Property.Community.Status.ToString()),
            })
            .ToListAsync(ct);
        var properties = links.Where(l => l.Status != CommunityStatus.Onboarding).Select(l => l.Summary).ToList();

        var memberships = await EffectiveMemberships(user.Id)
            .OrderBy(m => m.Community.CommunityName)
            .Select(m => new MembershipSummaryDto(
                m.CommunityId, m.Community.CommunityName, m.Role.ToString(), m.Community.Status.ToString()))
            .ToListAsync(ct);

        var isCompanyAdministrator = await db.CompanyAdministratorGrants
            .AnyAsync(g => g.UserId == user.Id && g.RevokedAt == null, ct);

        // FR-023: when the user holds no active non-resident membership, they are
        // returned to resident mode on their next request — even if LastActiveMode
        // is still Board (e.g. a board term expired mid-session). Reporting Board
        // without an active membership would strand them in a board shell with no
        // way out, since every board endpoint would then deny them.
        // 030 FR-036a: a user with no usable home has no resident mode to be in, so a board
        // membership puts them in board mode whatever they last chose.
        var hasBoardMembership = memberships.Any(m => m.Role != CommunityRole.Resident.ToString());
        var effectiveMode = hasBoardMembership && (user.LastActiveMode == UserMode.Board || properties.Count == 0)
            ? UserMode.Board
            : UserMode.Resident;

        var landing = LandingRule.Decide(new LandingRule.Input(
            HasUsableHome: properties.Count > 0,
            HasOnlyOnboardingHomes: links.Count > 0 && properties.Count == 0,
            HasBoardMembership: hasBoardMembership,
            IsCompanyAdministrator: isCompanyAdministrator,
            LastActiveMode: user.LastActiveMode,
            CompanySignInPage: false));

        return new CurrentUserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email!,
            $"{user.FirstName[0]}{user.LastName[0]}",
            properties,
            effectiveMode.ToString(),
            memberships,
            isCompanyAdministrator,
            landing.ToString());
    }

    // 030 R11: the home a sign-in selects — Active first, then Archived, oldest link first so the
    // choice is stable, never Onboarding. Null when the user has no usable home; they still sign in.
    private Task<Domain.Entities.Property?> FindUsablePropertyAsync(string userId, CancellationToken ct) =>
        db.UserProperties
            .Where(up => up.UserId == userId && up.Property.Community.Status != CommunityStatus.Onboarding)
            .OrderBy(up => up.Property.Community.Status == CommunityStatus.Active ? 0 : 1)
            .ThenBy(up => up.LinkedAt)
            .Select(up => up.Property)
            .FirstOrDefaultAsync(ct);

    private async Task<AuthResult> CreateTokenPairAsync(ApplicationUser user, Domain.Entities.Property? property, CancellationToken ct)
    {
        var expiry = DateTimeOffset.UtcNow.AddMinutes(AccessTokenExpiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        // 030 FR-036a: a user with no usable home signs in with no home claims.
        if (property is not null)
        {
            claims.Add(new Claim("propertyId", property.Id.ToString()));
            claims.Add(new Claim("communityId", property.CommunityId.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: expiry.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Rotating refresh token — store hash only
        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawRefresh),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays)
        });
        await db.SaveChangesAsync(ct);

        var currentUser = await BuildCurrentUserAsync(user, ct);

        return new AuthResult(new AuthResponse(accessToken, expiry, currentUser), rawRefresh);
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public class DomainException(
    string code, string message, int statusCode, IReadOnlyDictionary<string, object?>? extensions = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;

    // 030: extra body members for codes that carry detail, such as OPEN_WORK's items[] or
    // INVITATION_PENDING's invitationId. Written beside code and message.
    public IReadOnlyDictionary<string, object?>? Extensions { get; } = extensions;

    public Dictionary<string, object?> ToBody()
    {
        var body = new Dictionary<string, object?> { ["code"] = Code, ["message"] = Message };
        if (Extensions is not null)
            foreach (var (key, value) in Extensions)
                body[key] = value;
        return body;
    }
}
