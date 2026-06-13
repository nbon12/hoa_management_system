using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Configuration;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

public class JobsOptionsValidatorTests
{
    private static readonly JobsOptionsValidator Validator = new();

    [Fact]
    public void PresentSecret_Passes()
    {
        var o = new JobsOptions { SchedulerSharedSecret = "some-shared-secret" };
        Assert.True(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingSecret_Fails(string value)
    {
        var o = new JobsOptions { SchedulerSharedSecret = value };
        Assert.False(Validator.Validate(o).IsValid);
    }
}
