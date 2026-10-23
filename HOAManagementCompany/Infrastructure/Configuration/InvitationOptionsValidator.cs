using FluentValidation;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="InvitationOptions"/> at startup (constitution §8 — fail fast on invalid
/// configuration). The expiry is a spec constant (FR-022); the caps are tunable within bounds.
/// </summary>
public sealed class InvitationOptionsValidator : AbstractValidator<InvitationOptions>
{
    public InvitationOptionsValidator()
    {
        RuleFor(x => x.ExpiryHours)
            .Equal(72)
            .WithMessage("Invitations:ExpiryHours must be 72 (spec 030 FR-022).");
        RuleFor(x => x.MaxEmailsPerRecipientPer24h)
            .InclusiveBetween(1, 50)
            .WithMessage("Invitations:MaxEmailsPerRecipientPer24h must be between 1 and 50.");
        RuleFor(x => x.ResendCooldownSeconds)
            .InclusiveBetween(0, 3600)
            .WithMessage("Invitations:ResendCooldownSeconds must be between 0 and 3600.");
    }
}
