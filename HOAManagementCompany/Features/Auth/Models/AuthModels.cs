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

public record RefreshRequest(string RefreshToken);

public record SwitchPropertyRequest(Guid PropertyId);

public record AuthResponse(string Token, string RefreshToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

public record CurrentUserDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string Initials,
    IEnumerable<PropertySummaryDto> Properties);

public record PropertySummaryDto(Guid Id, string AccountNumber, string Address);
