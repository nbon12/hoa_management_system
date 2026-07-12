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
    IEnumerable<PropertySummaryDto> Properties);

public record PropertySummaryDto(Guid Id, string AccountNumber, string Address);
