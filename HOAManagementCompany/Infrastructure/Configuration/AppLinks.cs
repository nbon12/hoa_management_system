using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// The frontend origin used for links in emails (030 research R10). Bound from <c>"App"</c>; when
/// <c>App:BaseUrl</c> is unset it falls back to the older <c>ArchitecturalReview:AppBaseUrl</c>, which
/// the deployed environments already set, so both features read one value.
/// </summary>
public sealed class AppLinksOptions
{
    public const string SectionName = "App";
    public const string LegacyKey = "ArchitecturalReview:AppBaseUrl";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Resolved at startup from the host environment: true in the deployed environments (Dev,
    /// Staging, Production), where links must be absolute HTTPS. Local Development and Test may
    /// leave the URL empty, which yields relative links.
    /// </summary>
    public bool RequireAbsoluteHttps { get; set; }

    public void ApplyEnvironment(IConfiguration configuration, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            BaseUrl = configuration[LegacyKey] ?? string.Empty;
        RequireAbsoluteHttps = !environment.IsDevelopment() && !environment.IsEnvironment("Test");
    }
}

/// <summary>Builds absolute frontend links from <see cref="AppLinksOptions.BaseUrl"/>.</summary>
public sealed class AppLinks(IOptions<AppLinksOptions> options)
{
    public string BaseUrl => options.Value.BaseUrl.TrimEnd('/');

    /// <summary><paramref name="path"/> starts with "/" (for example "/invite#t=…").</summary>
    public string Link(string path) => $"{BaseUrl}{path}";
}
