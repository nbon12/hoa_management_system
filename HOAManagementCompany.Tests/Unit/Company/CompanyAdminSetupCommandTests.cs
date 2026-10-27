using HOAManagementCompany.Features.Company;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Company;

/// <summary>030 T059 — the setup step's command line (contracts/company.md § Setup step).</summary>
public class CompanyAdminSetupCommandTests
{
    [Fact]
    public void WithoutTheVerb_IsNotTheSetupStep()
    {
        Assert.Null(CompanyAdminSetupCommand.TryParse(["--seed"], out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TheVerb_ReadsTheEmailRecoverAndCompanyName()
    {
        var command = CompanyAdminSetupCommand.TryParse(
            ["--grant-company-admin", "owner@example.com", "--company-name", "Juniper Row Community Management"], out var error);

        Assert.Null(error);
        Assert.Equal(new CompanyAdminSetupCommand("owner@example.com", false, "Juniper Row Community Management"), command);
        Assert.True(CompanyAdminSetupCommand.TryParse(["--grant-company-admin", "owner@example.com", "--recover"], out _)!.Recover);
    }

    [Theory]
    [InlineData(new[] { "--grant-company-admin" }, "needs an email address")]
    [InlineData(new[] { "--grant-company-admin", "--recover" }, "needs an email address")]
    [InlineData(new[] { "--grant-company-admin", "owner@example.com", "--company-name" }, "--company-name needs a name")]
    public void MissingValues_AreErrors(string[] args, string message)
    {
        Assert.Null(CompanyAdminSetupCommand.TryParse(args, out var error));
        Assert.Contains(message, error);
    }
}
