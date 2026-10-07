using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Configuration;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

/// <summary>
/// SES email is optional (026 US3, 008 FR-012): all-empty disables it (valid); once any field is set,
/// each missing/invalid piece is rejected separately, and no message ever echoes a credential value.
/// </summary>
public class SesOptionsValidatorTests
{
    private static readonly SesOptionsValidator Validator = new();

    private const string Region = "us-east-1";
    private const string From = "no-reply@mail.nekohoa.com";

    // region, fromEmail, accessKeyId, secretAccessKey, expectedValid
    [Theory]
    [InlineData("", "", "", "", true)]                              // fully empty → disabled, valid
    [InlineData(Region, From, "", "", true)]                        // default credential chain
    [InlineData(Region, From, "AKIAEXAMPLE", "secret", true)]       // static keys (CI)
    [InlineData(Region, "", "", "", false)]                         // region without sender
    [InlineData("", From, "", "", false)]                           // sender without region
    [InlineData("not-a-region", From, "", "", false)]               // unknown region
    [InlineData(Region, "not-an-email", "", "", false)]             // invalid sender
    [InlineData(Region, From, "AKIAEXAMPLE", "", false)]            // key id without secret
    [InlineData(Region, From, "", "secret", false)]                 // secret without key id
    [InlineData("", "", "AKIAEXAMPLE", "secret", false)]            // keys only
    public void PartialConfig_RejectedFullOrEmptyAccepted(
        string region, string fromEmail, string accessKeyId, string secretAccessKey, bool expectedValid)
    {
        var o = new SesOptions
        {
            Region = region,
            FromEmail = fromEmail,
            AccessKeyId = accessKeyId,
            SecretAccessKey = secretAccessKey,
        };
        Assert.Equal(expectedValid, Validator.Validate(o).IsValid);
    }

    [Fact]
    public void Each_missing_required_setting_is_reported_separately()
    {
        var result = Validator.Validate(new SesOptions { AccessKeyId = "AKIAEXAMPLE", SecretAccessKey = "secret" });

        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Ses:Region"));
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Ses:FromEmail"));
    }

    [Fact]
    public void Messages_never_echo_credential_values()
    {
        var result = Validator.Validate(new SesOptions
        {
            Region = Region,
            FromEmail = From,
            AccessKeyId = "AKIASECRETVALUE1",
        });

        var error = Assert.Single(result.Errors);
        Assert.Contains("must be set together", error.ErrorMessage);
        Assert.DoesNotContain("AKIASECRETVALUE1", error.ErrorMessage);
    }
}
