using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Company;

/// <summary>030 T046 — invitation link tokens (research R6; contracts/invitations-and-auth.md § Invitation links).</summary>
public class InvitationTokensTests
{
    [Fact]
    public void NewToken_Is43Base64UrlCharacters()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => InvitationTokens.New()).ToList();

        Assert.All(tokens, t =>
        {
            Assert.Equal(43, t.Length);
            Assert.Matches("^[A-Za-z0-9_-]{43}$", t);
        });
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    [Fact]
    public void Hash_IsTheAuthCryptoHash()
    {
        var token = InvitationTokens.New();

        Assert.Equal(AuthCrypto.Hash(token), InvitationTokens.Hash(token));
        Assert.Equal(64, InvitationTokens.Hash(token).Length);
    }

    [Fact]
    public void Link_PutsTheTokenInTheFragment()
    {
        var links = new AppLinks(Options.Create(new AppLinksOptions { BaseUrl = "https://app.example.com/" }));
        var token = InvitationTokens.New();

        var link = InvitationTokens.Link(links, token);

        Assert.Equal($"https://app.example.com/invite#t={token}", link);
        var uri = new Uri(link);
        Assert.Equal("/invite", uri.AbsolutePath);
        Assert.Equal("", uri.Query);
        Assert.Equal($"#t={token}", uri.Fragment);
    }
}
