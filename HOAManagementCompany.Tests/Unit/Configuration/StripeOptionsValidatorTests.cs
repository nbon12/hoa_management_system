using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Configuration;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

public class StripeOptionsValidatorTests
{
    private static readonly StripeOptionsValidator Validator = new();

    private static StripeOptions Valid() => new()
    {
        SecretKey = "sk_test_placeholder",
        PublishableKey = "pk_test_placeholder",
        WebhookSigningSecret = "whsec_placeholder",
        WebhookToleranceSeconds = 300,
    };

    [Fact]
    public void DefaultValidConfig_Passes() => Assert.True(Validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingSecretKey_Fails(string value)
    {
        var o = Valid();
        o.SecretKey = value;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingPublishableKey_Fails(string value)
    {
        var o = Valid();
        o.PublishableKey = value;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingWebhookSigningSecret_Fails(string value)
    {
        var o = Valid();
        o.WebhookSigningSecret = value;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveWebhookTolerance_Fails(long seconds)
    {
        var o = Valid();
        o.WebhookToleranceSeconds = seconds;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(300)]
    public void PositiveWebhookTolerance_Passes(long seconds)
    {
        var o = Valid();
        o.WebhookToleranceSeconds = seconds;
        Assert.True(Validator.Validate(o).IsValid);
    }
}
