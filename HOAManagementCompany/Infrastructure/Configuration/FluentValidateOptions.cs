// <!-- REPOWISE:START domain=configuration -->
// Generic bridge from FluentValidation to the options-validation pipeline. Lets any
// AbstractValidator<TOptions> run as an IValidateOptions<TOptions>, so strongly-typed
// configuration is validated at startup (paired with ValidateOnStart) instead of failing
// later at first use (008-config-validation, FR-001/FR-013). Failure messages name the
// option type + rule and never echo raw values (FR-019).
// <!-- REPOWISE:END -->

using FluentValidation;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Adapts a FluentValidation <see cref="IValidator{T}"/> to <see cref="IValidateOptions{T}"/>
/// so options groups are validated through the standard options pipeline. FluentValidation
/// already collects <em>all</em> rule failures for the instance, so a single misconfigured
/// section reports every problem at once (FR-003).
/// </summary>
public sealed class FluentValidateOptions<T> : IValidateOptions<T> where T : class
{
    private readonly string? _name;
    private readonly IValidator<T> _validator;

    /// <param name="name">
    /// The named-options instance this validator applies to, or <c>null</c> to validate every
    /// instance. Matches the <see cref="IValidateOptions{T}"/> contract.
    /// </param>
    public FluentValidateOptions(string? name, IValidator<T> validator)
    {
        _name = name;
        _validator = validator;
    }

    public ValidateOptionsResult Validate(string? name, T options)
    {
        // Honor named options: skip instances this validator was not registered for.
        if (_name is not null && _name != name)
            return ValidateOptionsResult.Skip;

        ArgumentNullException.ThrowIfNull(options);

        var result = _validator.Validate(options);
        if (result.IsValid)
            return ValidateOptionsResult.Success;

        // Prefix each failure with the options type so the aggregate startup error makes the
        // offending section obvious. ErrorMessage is rule text (e.g. "must be CreditOnly"),
        // never the attempted value, keeping secrets out of logs (FR-019).
        var failures = result.Errors
            .Select(e => $"{typeof(T).Name}.{e.PropertyName}: {e.ErrorMessage}")
            .ToArray();

        return ValidateOptionsResult.Fail(failures);
    }
}
