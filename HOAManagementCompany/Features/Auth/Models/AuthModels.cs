namespace HOAManagementCompany.Features.Auth.Models;

// 016-A FR-A1/A3: registration binds to a property via an email-verification proof + a single-use
// claim code. The email is taken from the verified record, not re-supplied by the caller.
public record RegisterRequest(
    string VerificationToken,
    string Password,
    string FirstName,
    string LastName,
    string ClaimCode);

public record VerifyEmailRequest(string Email);
public record VerifyEmailRequestResponse(string Status);
public record VerifyEmailConfirmRequest(string Email, string Code);
public record VerifyEmailConfirmResponse(string VerificationToken);

public record LoginRequest(string Email, string Password);

public record SwitchPropertyRequest(Guid PropertyId);

// 025 FR-018/FR-022: switch the caller's active UI mode. Mode is UX state, never an
// authorization input (FR-014).
public record BoardModeRequest(string Mode);

// 020-D FR-D1: the refresh token is transported only in an HttpOnly cookie — it must never
// appear in a response body, so AuthResponse deliberately has no RefreshToken member.
public record AuthResponse(string Token, DateTimeOffset ExpiresAt, CurrentUserDto User);

// Internal pairing of the client-safe response with the raw refresh token the endpoint puts in
// the cookie. Never serialized.
public sealed record AuthResult(AuthResponse Response, string RefreshToken);

public record CurrentUserDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string Initials,
    IEnumerable<PropertySummaryDto> Properties,
    // 025: last-used UI mode (FR-022) and the caller's active community memberships.
    // The frontend derives mode-toggle visibility (FR-020) and the board nav set
    // (FR-024/FR-025) from these, with no extra endpoint (research.md R6).
    string LastActiveMode,
    IEnumerable<MembershipSummaryDto> Memberships);

public record PropertySummaryDto(Guid Id, string AccountNumber, string Address);

public record MembershipSummaryDto(Guid CommunityId, string CommunityName, string Role);
