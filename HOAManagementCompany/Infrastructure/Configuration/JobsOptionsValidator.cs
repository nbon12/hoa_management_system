using FluentValidation;
using HOAManagementCompany.Features.Payments;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="JobsOptions"/> at startup. The scheduler shared secret guards the
/// internal job endpoints and is required in every environment (008 FR-004).
/// </summary>
public sealed class JobsOptionsValidator : AbstractValidator<JobsOptions>
{
    public JobsOptionsValidator()
    {
        RuleFor(x => x.SchedulerSharedSecret).NotEmpty()
            .WithMessage("Jobs:SchedulerSharedSecret is required.");
    }
}
