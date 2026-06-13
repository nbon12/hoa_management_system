using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Configuration;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

/// <summary>
/// Optional alert providers (008 FR-012): all-empty disables them (valid); a partially-configured
/// provider that would fail at send time is rejected.
/// </summary>
public class TwilioSendGridOptionsValidatorTests
{
    private static readonly TwilioOptionsValidator Twilio = new();
    private static readonly SendGridOptionsValidator SendGrid = new();

    // ── Twilio ──────────────────────────────────────────────────────────────────────────
    // account, apiKeySid, apiKeySecret, authToken, from, expectedValid
    [Theory]
    [InlineData("", "", "", "", "", true)]                                  // fully empty → disabled, valid
    [InlineData("AC123", "SK1", "secret", "", "+15005550006", true)]        // API-key auth (prod)
    [InlineData("AC123", "", "", "tok", "+15005550006", true)]              // basic auth (sandbox)
    [InlineData("AC123", "", "", "", "+15005550006", false)]               // no usable auth
    [InlineData("AC123", "SK1", "", "", "+15005550006", false)]            // api-key sid without secret
    [InlineData("AC123", "SK1", "secret", "", "", false)]                  // missing from-number
    [InlineData("", "SK1", "secret", "", "+15005550006", false)]           // missing account sid
    public void Twilio_PartialConfig_RejectedFullOrEmptyAccepted(
        string accountSid, string apiKeySid, string apiKeySecret, string authToken, string from, bool expectedValid)
    {
        var o = new TwilioOptions
        {
            AccountSid = accountSid,
            ApiKeySid = apiKeySid,
            ApiKeySecret = apiKeySecret,
            AuthToken = authToken,
            FromNumber = from,
        };
        Assert.Equal(expectedValid, Twilio.Validate(o).IsValid);
    }

    // ── SendGrid ────────────────────────────────────────────────────────────────────────
    // apiKey, fromEmail, expectedValid
    [Theory]
    [InlineData("", "", true)]                              // fully empty → disabled, valid
    [InlineData("SG.key", "alerts@nekohoa.com", true)]      // fully configured, valid email
    [InlineData("SG.key", "", false)]                       // api key without from-email
    [InlineData("", "alerts@nekohoa.com", false)]           // from-email without api key
    [InlineData("SG.key", "not-an-email", false)]           // invalid from-email
    public void SendGrid_PartialConfig_RejectedFullOrEmptyAccepted(
        string apiKey, string fromEmail, bool expectedValid)
    {
        var o = new SendGridOptions
        {
            ApiKey = apiKey,
            FromEmail = fromEmail,
        };
        Assert.Equal(expectedValid, SendGrid.Validate(o).IsValid);
    }
}
