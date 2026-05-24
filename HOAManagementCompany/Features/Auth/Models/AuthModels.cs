namespace HOAManagementCompany.Features.Auth.Models;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string AccountNumber);

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
