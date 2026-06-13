using System.Security.Cryptography;
using System.Text;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// Builds a Stripe-signed webhook payload in-process (FR-012), so the webhook signature → persistence
/// path is verified with no Stripe CLI sidecar (Clarifications Q1). Signature verification is pure
/// local HMAC-SHA256 against the endpoint's signing secret, so a fixture signed with the real test
/// <c>whsec_…</c> is cryptographically identical to a Stripe-delivered event.
///
/// <para>
/// The header format matches Stripe's: <c>t=&lt;unix&gt;,v1=&lt;hex HMAC-SHA256("&lt;t&gt;.&lt;payload&gt;", secret)&gt;</c>.
/// The HMAC key is the configured secret string exactly as Stripe.net uses it (prefix included).
/// Set <paramref name="tamper"/> to corrupt <c>v1</c> for the negative case while keeping the header
/// well-formed.
/// </para>
/// </summary>
public sealed class SignedWebhookFixture
{
    public string Payload { get; }
    public long Timestamp { get; }
    public string SignatureHeader { get; }

    public SignedWebhookFixture(string payload, string signingSecret, bool tamper = false, long? timestamp = null)
    {
        Payload = payload;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var signature = ComputeSignature(signingSecret, $"{Timestamp}.{payload}");
        if (tamper)
            signature = Corrupt(signature);

        SignatureHeader = $"t={Timestamp},v1={signature}";
    }

    private static string ComputeSignature(string secret, string signedPayload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Flip the leading hex digit: verification fails, header stays syntactically valid.
    private static string Corrupt(string signature) =>
        (signature[0] == '0' ? '1' : '0') + signature[1..];
}
