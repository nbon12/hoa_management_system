using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Configuration;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

/// <summary>
/// Optional Twilio SMS provider (008 FR-012; SES is covered by SesOptionsValidatorTests): all-empty disables it (valid); a partially-configured
/// provider that would fail at send time is rejected.
/// </summary>
public class TwilioOptionsValidatorTests
{
    private static readonly TwilioOptionsValidator Twilio = new();

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
}
