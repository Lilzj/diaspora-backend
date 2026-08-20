using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Infrastructure.Identity.Tokens;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Identity.Security;

public class RefreshTokenIssuerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static RefreshTokenIssuer CreateIssuer(int sizeBytes = 32, int lifetimeDays = 30) =>
        new(Options.Create(new RefreshTokenOptions
        {
            TokenSizeBytes = sizeBytes,
            LifetimeDays = lifetimeDays
        }));

    [Fact]
    public void Issued_tokens_are_unique()
    {
        var issuer = CreateIssuer();

        var tokens = Enumerable.Range(0, 500).Select(_ => issuer.Issue(Now).Token).ToArray();

        tokens.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Tokens_are_url_safe()
    {
        var token = CreateIssuer().Issue(Now).Token;

        token.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
    }

    [Fact]
    public void Tokens_carry_the_configured_entropy()
    {
        var token = CreateIssuer(sizeBytes: 32).Issue(Now).Token;

        // 32 bytes base64url-encoded, padding stripped.
        token.Length.Should().Be(43);
    }

    [Fact]
    public void The_expiry_follows_the_configured_lifetime()
    {
        var descriptor = CreateIssuer(lifetimeDays: 14).Issue(Now);

        descriptor.ExpiresAt.Should().Be(Now.AddDays(14));
    }

    [Fact]
    public void The_hash_matches_the_token_it_was_derived_from()
    {
        var issuer = CreateIssuer();
        var descriptor = issuer.Issue(Now);

        issuer.ComputeHash(descriptor.Token).Should().Be(descriptor.Hash);
    }

    [Fact]
    public void Hashing_is_deterministic_so_it_can_serve_as_a_lookup_key()
    {
        var issuer = CreateIssuer();

        issuer.ComputeHash("a-known-token").Should().Be(issuer.ComputeHash("a-known-token"));
    }

    [Fact]
    public void Different_tokens_hash_differently()
    {
        var issuer = CreateIssuer();

        issuer.ComputeHash("token-a").Should().NotBe(issuer.ComputeHash("token-b"));
    }

    [Fact]
    public void The_stored_hash_does_not_reveal_the_token()
    {
        var issuer = CreateIssuer();
        var descriptor = issuer.Issue(Now);

        descriptor.Hash.Value.Should().NotContain(descriptor.Token);
        descriptor.Hash.ToString().Should().Be("***", "the hash must not render into logs");
    }
}
