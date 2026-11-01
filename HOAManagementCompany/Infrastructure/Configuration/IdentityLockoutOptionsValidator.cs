using FluentValidation;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="IdentityLockoutOptions"/> at startup (constitution §8 — fail fast on invalid
/// configuration). A zero or negative threshold would silently weaken or disable lockout.
/// </summary>
public sealed class IdentityLockoutOptionsValidator : AbstractValidator<IdentityLockoutOptions>
{
    public IdentityLockoutOptionsValidator()
    {
        RuleFor(x => x.MaxFailedAttempts)
            .InclusiveBetween(1, 100)
            .WithMessage("Identity:Lockout:MaxFailedAttempts must be between 1 and 100.");
        RuleFor(x => x.LockoutMinutes)
            .InclusiveBetween(1, 1440)
            .WithMessage("Identity:Lockout:LockoutMinutes must be between 1 and 1440.");
    }
}
