using HOAManagementCompany.Infrastructure.Configuration;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

/// <summary>
/// Unit tests for <see cref="HostEnvironmentOptionsValidator"/> (010, constitution §8): the host
/// environment name must be one of the exact known set; mis-set or mis-cased values are rejected.
/// </summary>
public class HostEnvironmentOptionsValidatorTests
{
    private static readonly HostEnvironmentOptionsValidator Validator = new();

    private static HostEnvironmentOptions Of(string name) => new() { EnvironmentName = name };

    [Theory]
    [InlineData("Development")] // local
    [InlineData("Dev")]         // deployed dev (010)
    [InlineData("Test")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void KnownEnvironments_Pass(string name) =>
        Assert.True(Validator.Validate(Of(name)).IsValid);

    [Theory]
    [InlineData("prod")]        // the classic mis-set — must be "Production"
    [InlineData("production")]  // wrong casing → ASP.NET would not treat as Production
    [InlineData("development")] // wrong casing
    [InlineData("dev")]         // wrong casing
    [InlineData("Prod")]        // title(env_name) shortcut would wrongly produce this
    [InlineData("Local")]       // not a known environment
    [InlineData("")]            // unset
    [InlineData("   ")]         // whitespace
    public void UnknownOrMisCasedEnvironments_Fail(string name) =>
        Assert.False(Validator.Validate(Of(name)).IsValid);

    [Fact]
    public void FailureMessage_ListsAllowedEnvironments()
    {
        var result = Validator.Validate(Of("prod"));
        Assert.False(result.IsValid);
        var message = Assert.Single(result.Errors).ErrorMessage;
        Assert.Contains("Production", message);
        Assert.Contains("Dev", message);
    }
}
