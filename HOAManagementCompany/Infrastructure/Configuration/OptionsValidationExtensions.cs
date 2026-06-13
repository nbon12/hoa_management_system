// <!-- REPOWISE:START domain=configuration -->
// Registration helper that binds a configuration section to a strongly-typed options class,
// attaches its FluentValidation validator via FluentValidateOptions<T>, and calls
// ValidateOnStart so the host refuses to boot on invalid configuration (008-config-validation,
// FR-001). Replaces the unvalidated builder.Services.Configure<T>(section) calls in Program.cs.
// <!-- REPOWISE:END -->

using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Infrastructure.Configuration;

public static class OptionsValidationExtensions
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to <paramref name="sectionName"/>, registers
    /// <typeparamref name="TValidator"/> as its FluentValidation validator, and validates the
    /// bound instance at application start. A failure throws
    /// <see cref="OptionsValidationException"/> during host startup with all rule violations
    /// for the section (FR-002/FR-003), so misconfiguration fails fast instead of at first use.
    /// </summary>
    public static IServiceCollection AddValidatedOptions<TOptions, TValidator>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
        where TValidator : class, IValidator<TOptions>
    {
        services.AddSingleton<IValidator<TOptions>, TValidator>();

        // The adapter is registered for the default (unnamed) options instance.
        services.AddSingleton<IValidateOptions<TOptions>>(sp =>
            new FluentValidateOptions<TOptions>(
                Microsoft.Extensions.Options.Options.DefaultName,
                sp.GetRequiredService<IValidator<TOptions>>()));

        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateOnStart();

        return services;
    }
}
