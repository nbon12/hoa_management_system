using HOAManagementCompany.Features.Payments;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Payments;

/// <summary>
/// <see cref="TwilioOptions.IsConfigured"/> must accept either auth pairing the adapter supports:
/// API-key auth (<c>ApiKeySid</c> + <c>ApiKeySecret</c>) in production, or basic auth
/// (<c>AuthToken</c>) for the Stage 2 (007) test-credential path. An account SID and from-number are
/// always required, and an Auth-Token-only test credential must not be reported as "not configured".
/// </summary>
public class TwilioOptionsTests
{
    [Theory]
    // account, apiKeySid, apiKeySecret, authToken, from, expected
    [InlineData("AC123", "SK1", "secret", "", "+15005550006", true)]   // API-key auth (production)
    [InlineData("AC123", "", "", "tok", "+15005550006", true)]         // basic auth (sandbox)
    [InlineData("AC123", "SK1", "secret", "tok", "+15005550006", true)] // both present
    [InlineData("AC123", "", "", "", "+15005550006", false)]           // no usable auth
    [InlineData("AC123", "SK1", "", "", "+15005550006", false)]        // api-key sid without secret, no token
    [InlineData("", "SK1", "secret", "tok", "+15005550006", false)]    // missing account sid
    [InlineData("AC123", "SK1", "secret", "tok", "", false)]           // missing from-number
    public void IsConfigured_AcceptsEitherAuthPath(
        string accountSid, string apiKeySid, string apiKeySecret, string authToken, string from, bool expected)
    {
        var opts = new TwilioOptions
        {
            AccountSid = accountSid,
            ApiKeySid = apiKeySid,
            ApiKeySecret = apiKeySecret,
            AuthToken = authToken,
            FromNumber = from,
        };

        Assert.Equal(expected, opts.IsConfigured);
    }
}
