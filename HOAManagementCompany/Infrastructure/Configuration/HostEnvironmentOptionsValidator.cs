using FluentValidation;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Asserts the host <c>ASPNETCORE_ENVIRONMENT</c> name is in <see cref="HostEnvironmentOptions.KnownEnvironments"/>
/// at startup so a mis-set value fails the host fast (010, constitution §8). Catches the common
/// confusions the IaC could introduce: <c>prod</c> instead of <c>Production</c>, or deployed-<c>Dev</c>
/// vs local-<c>Development</c>. The match is case-sensitive on purpose: <c>production</c> is NOT
/// <c>Production</c> to ASP.NET Core, so allowing it would let <c>IsProduction()</c> return false in a
/// production deployment.
/// </summary>
public sealed class HostEnvironmentOptionsValidator : AbstractValidator<HostEnvironmentOptions>
{
    public HostEnvironmentOptionsValidator()
    {
        RuleFor(x => x.EnvironmentName)
            .Must(name => HostEnvironmentOptions.KnownEnvironments.Contains(name))
            .WithMessage(o =>
                $"ASPNETCORE_ENVIRONMENT '{o.EnvironmentName}' is not a known environment. " +
                $"Expected one of: {string.Join(", ", HostEnvironmentOptions.KnownEnvironments)} " +
                "(case-sensitive; e.g. use 'Production', not 'prod'; deployed dev is 'Dev', local is 'Development').");
    }
}
