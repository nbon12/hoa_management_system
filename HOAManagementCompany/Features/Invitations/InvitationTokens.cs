using System.Security.Cryptography;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Infrastructure.Configuration;

namespace HOAManagementCompany.Features.Invitations;

/// <summary>
/// Invitation link tokens (030 research R6). A token is 32 random bytes, base64url-encoded without
/// padding (43 characters, 256 bits). Only its SHA-256 hash is stored. The link puts it in the URL
/// fragment, which browsers never send to a server, so it stays out of server and edge logs.
/// </summary>
public static class InvitationTokens
{
    public static string New() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) => AuthCrypto.Hash(token);

    public static string Link(AppLinks links, string token) => links.Link($"/invite#t={token}");
}
