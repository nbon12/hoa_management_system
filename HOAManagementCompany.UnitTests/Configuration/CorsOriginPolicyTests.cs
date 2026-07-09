using HOAManagementCompany.Infrastructure.Configuration;
using Xunit;

namespace HOAManagementCompany.UnitTests.Configuration;

public class CorsOriginPolicyTests
{
    private static readonly string[] Exact = ["https://dev.nekohoa.com"];
    private static readonly string[] Suffixes = [".nekohoa-dev.pages.dev"];

    [Fact]
    public void Allows_exact_origin()
        => Assert.True(CorsOriginPolicy.IsAllowed("https://dev.nekohoa.com", Exact, Suffixes));

    [Fact]
    public void Allows_exact_origin_case_insensitively()
        => Assert.True(CorsOriginPolicy.IsAllowed("https://DEV.NEKOHOA.COM", Exact, Suffixes));

    [Fact]
    public void Allows_pages_preview_origin_under_suffix()
        => Assert.True(CorsOriginPolicy.IsAllowed("https://abc123.nekohoa-dev.pages.dev", Exact, Suffixes));

    [Fact]
    public void Rejects_lookalike_host_that_only_appears_to_match_suffix()
        => Assert.False(CorsOriginPolicy.IsAllowed("https://evilnekohoa-dev.pages.dev", Exact, Suffixes));

    [Fact]
    public void Rejects_unrelated_origin()
        => Assert.False(CorsOriginPolicy.IsAllowed("https://evil.com", Exact, Suffixes));

    [Fact]
    public void Rejects_suffix_match_when_no_suffixes_configured()
        => Assert.False(CorsOriginPolicy.IsAllowed("https://abc123.nekohoa-dev.pages.dev", Exact, []));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    public void Rejects_empty_or_malformed_origin(string? origin)
        => Assert.False(CorsOriginPolicy.IsAllowed(origin, Exact, Suffixes));
}
