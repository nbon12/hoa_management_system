using System.Security.Cryptography;
using System.Text;

namespace HOAManagementCompany.Features.Auth;

// 016-A: shared hashing + secret generation for verification codes, claim codes, and proofs.
public static class AuthCrypto
{
    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    public static bool HashesEqual(string hashA, string hashB) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hashA), Encoding.UTF8.GetBytes(hashB));

    // Short numeric code for user-typed email verification.
    public static string NewNumericCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    // Longer claim code delivered out-of-band (Crockford-ish base32, grouped for readability).
    public static string NewClaimCode()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789"; // no ambiguous chars
        var chars = new char[10];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return $"{new string(chars, 0, 5)}-{new string(chars, 5, 5)}";
    }

    // High-entropy opaque proof token (email-verification → register handoff).
    public static string NewProofToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
