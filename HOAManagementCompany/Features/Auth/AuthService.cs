using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HOAManagementCompany.Domain.Entities;
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

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
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

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
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
        var property = await GetActivePropertyAsync(user.Id, ct);
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

    public async Task<AuthResponse> RefreshAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = HashToken(rawToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || !stored.IsActive)
            throw new DomainException("INVALID_REFRESH_TOKEN", "The refresh token is invalid or has expired.", 401);

        db.RefreshTokens.Remove(stored);
        await db.SaveChangesAsync(ct);

        var property = await GetActivePropertyAsync(stored.UserId, ct);
        return await CreateTokenPairAsync(stored.User, property, ct);
    }

    public async Task<AuthResponse> SwitchPropertyAsync(string userId, Guid propertyId, CancellationToken ct = default)
    {
        var link = await db.UserProperties
            .Include(up => up.Property)
            .FirstOrDefaultAsync(up => up.UserId == userId && up.PropertyId == propertyId, ct);

        if (link is null)
            throw new DomainException("PROPERTY_ACCESS_DENIED", "You are not linked to the requested property.", 403);

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

        var properties = await db.UserProperties
            .Include(up => up.Property)
            .Where(up => up.UserId == userId)
            .Select(up => new PropertySummaryDto(up.Property.Id, up.Property.AccountNumber, up.Property.Address))
            .ToListAsync(ct);

        return new CurrentUserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email!,
            $"{user.FirstName[0]}{user.LastName[0]}",
            properties);
    }

    private async Task<Domain.Entities.Property> GetActivePropertyAsync(string userId, CancellationToken ct)
    {
        return await db.UserProperties
            .Include(up => up.Property)
            .Where(up => up.UserId == userId)
            .Select(up => up.Property)
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException("NO_PROPERTY", "User has no linked property.", 422);
    }

    private async Task<AuthResponse> CreateTokenPairAsync(ApplicationUser user, Domain.Entities.Property property, CancellationToken ct)
    {
        var expiry = DateTimeOffset.UtcNow.AddMinutes(AccessTokenExpiryMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim("propertyId", property.Id.ToString()),
            new Claim("communityId", property.CommunityId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

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

        var properties = await db.UserProperties
            .Include(up => up.Property)
            .Where(up => up.UserId == user.Id)
            .Select(up => new PropertySummaryDto(up.Property.Id, up.Property.AccountNumber, up.Property.Address))
            .ToListAsync(ct);

        var currentUser = new CurrentUserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email!,
            $"{user.FirstName[0]}{user.LastName[0]}",
            properties);

        return new AuthResponse(accessToken, rawRefresh, expiry, currentUser);
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public class DomainException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
