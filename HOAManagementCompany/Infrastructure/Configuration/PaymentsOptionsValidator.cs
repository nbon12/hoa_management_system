using System;
using FluentValidation;
using HOAManagementCompany.Features.Payments;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="PaymentsOptions"/> at startup, enforcing the fee-policy rules that were
/// previously only documented in comments (008 FR-006/FR-007): fee type and scope must be known
/// values, a percentage fee must be scoped to credit cards only, and all amounts/durations must
/// be non-negative.
/// </summary>
public sealed class PaymentsOptionsValidator : AbstractValidator<PaymentsOptions>
{
    private const StringComparison Ci = StringComparison.OrdinalIgnoreCase;

    public PaymentsOptionsValidator()
    {
        RuleFor(x => x.VariableNoticeLeadDays).GreaterThanOrEqualTo(0)
            .WithMessage("Payments:VariableNoticeLeadDays must be 0 or greater.");
        RuleFor(x => x.ReconcilePendingAchAfterHours).GreaterThan(0)
            .WithMessage("Payments:ReconcilePendingAchAfterHours must be greater than 0.");

        RuleFor(x => x.DefaultFee.CardFeeType)
            .Must(t => string.Equals(t, "Flat", Ci) || string.Equals(t, "Percentage", Ci))
            .WithMessage("Payments:DefaultFee:CardFeeType must be 'Flat' or 'Percentage'.");

        RuleFor(x => x.DefaultFee.CardScope)
            .Must(s => string.Equals(s, "AllCards", Ci) || string.Equals(s, "CreditOnly", Ci))
            .WithMessage("Payments:DefaultFee:CardScope must be 'AllCards' or 'CreditOnly'.");

        // Cross-field rule: a percentage fee may only apply to credit cards (CreditOnly).
        RuleFor(x => x.DefaultFee)
            .Must(f => !string.Equals(f.CardFeeType, "Percentage", Ci)
                       || string.Equals(f.CardScope, "CreditOnly", Ci))
            .WithMessage("Payments:DefaultFee — a Percentage CardFeeType requires CardScope 'CreditOnly'.");

        RuleFor(x => x.DefaultFee.CardFeeValue).GreaterThanOrEqualTo(0m)
            .WithMessage("Payments:DefaultFee:CardFeeValue must be 0 or greater.");
        RuleFor(x => x.DefaultFee.AchFeeValue).GreaterThanOrEqualTo(0m)
            .WithMessage("Payments:DefaultFee:AchFeeValue must be 0 or greater.");
        RuleFor(x => x.Nsf.Amount).GreaterThanOrEqualTo(0m)
            .WithMessage("Payments:Nsf:Amount must be 0 or greater.");
    }
}
