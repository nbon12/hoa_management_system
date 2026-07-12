using FluentValidation;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="RefreshCookieOptions"/> at startup (constitution §8 — fail fast on invalid
/// configuration). SameSite must be an explicit, known value: a typo silently falling back to a
/// browser default would either break cross-site environments (cookie never sent) or silently
/// weaken Production.
/// </summary>
public sealed class RefreshCookieOptionsValidator : AbstractValidator<RefreshCookieOptions>
{
    private static readonly string[] Allowed = ["Strict", "Lax", "None"];

    public RefreshCookieOptionsValidator()
    {
        RuleFor(x => x.SameSite)
            .Must(v => Allowed.Contains(v))
            .WithMessage("Auth:RefreshCookie:SameSite must be one of: Strict, Lax, None.");
    }
}
