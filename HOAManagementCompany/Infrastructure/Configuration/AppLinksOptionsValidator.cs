using FluentValidation;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="AppLinksOptions"/> at startup (constitution §8). Deployed environments
/// must set an absolute HTTPS origin, because emailed links that point nowhere fail silently.
/// Local Development and Test may leave it empty; when set there, it must still be absolute.
/// </summary>
public sealed class AppLinksOptionsValidator : AbstractValidator<AppLinksOptions>
{
    public AppLinksOptionsValidator()
    {
        RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .When(x => x.RequireAbsoluteHttps)
            .WithMessage("App:BaseUrl (or ArchitecturalReview:AppBaseUrl) must be set in deployed environments.");

        RuleFor(x => x.BaseUrl)
            .Must(BeAbsoluteHttpUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.BaseUrl))
            .WithMessage("App:BaseUrl must be an absolute http(s) URL.");

        RuleFor(x => x.BaseUrl)
            .Must(v => v.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .When(x => x.RequireAbsoluteHttps && !string.IsNullOrWhiteSpace(x.BaseUrl))
            .WithMessage("App:BaseUrl must use HTTPS outside local Development and Test.");
    }

    private static bool BeAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
