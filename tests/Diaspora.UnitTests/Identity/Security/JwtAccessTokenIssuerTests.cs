using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Infrastructure.Identity.Tokens;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Diaspora.UnitTests.Identity.Security;

public class JwtAccessTokenIssuerTests
{
    private const string SigningKey = "unit-test-signing-key-at-least-32-bytes-long-for-hmac-sha256";

    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    private static JwtAccessTokenIssuer CreateIssuer(bool includeFeatures = false) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "https://identity.diaspora.test",
            Audience = "https://api.diaspora.test",
            SigningKey = SigningKey,
            AccessTokenMinutes = 15,
            IncludeFeatureClaims = includeFeatures
        }), Clock);

    private static AccessTokenRequest CreateRequest(
        AccessState? state = null,
        IReadOnlyList<string>? permissions = null,
        IReadOnlyList<string>? features = null) =>
        new()
        {
            UserId = UserId.New(),
            SessionId = SessionId.New(),
            DeviceId = DeviceId.New(),
            Role = UserRole.CommunityUser,
            AccessState = state ?? AccessState.LimitedAccess,
            SecurityStamp = 7,
            Permissions = permissions ?? [IdentityPermissions.ReadOwnProfile],
            Features = features
        };

    // Read back with the same JsonWebTokens stack the issuer writes with, rather than
    // pulling in the legacy JwtSecurityTokenHandler purely for assertions.
    private static JsonWebToken Read(string token) => new(token);

    [Fact]
    public void The_token_carries_the_subject_and_session()
    {
        var request = CreateRequest();

        var descriptor = CreateIssuer().Issue(request);
        var token = Read(descriptor.Token);

        token.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Sub && c.Value == request.UserId.Value.ToString());
        token.Claims.Should().Contain(c =>
            c.Type == DiasporaClaimTypes.SessionId && c.Value == request.SessionId.Value.ToString());
    }

    [Fact]
    public void The_token_carries_the_access_state_and_security_stamp()
    {
        var descriptor = CreateIssuer().Issue(CreateRequest(AccessState.Verified));
        var token = Read(descriptor.Token);

        token.Claims.Should().Contain(c =>
            c.Type == DiasporaClaimTypes.AccessState && c.Value == nameof(AccessState.Verified));
        token.Claims.Should().Contain(c => c.Type == DiasporaClaimTypes.SecurityStamp && c.Value == "7");
    }

    [Fact]
    public void Permissions_are_emitted_one_claim_each()
    {
        // Any three permission strings; the issuer does not care what they mean, only that
        // it emits one claim each.
        var permissions = new[]
        {
            IdentityPermissions.ReadOwnProfile,
            IdentityPermissions.UpdateOwnProfile,
            IdentityPermissions.ChangeOwnPassword
        };

        var descriptor = CreateIssuer().Issue(CreateRequest(permissions: permissions));
        var token = Read(descriptor.Token);

        token.Claims.Where(c => c.Type == DiasporaClaimTypes.Permission)
            .Select(c => c.Value)
            .Should().BeEquivalentTo(permissions);
    }

    [Fact]
    public void Feature_claims_are_omitted_unless_configured()
    {
        var descriptor = CreateIssuer(includeFeatures: false)
            .Issue(CreateRequest(features: ["guides.browse"]));

        Read(descriptor.Token).Claims.Should().NotContain(c => c.Type == DiasporaClaimTypes.Feature);
    }

    [Fact]
    public void Feature_claims_are_emitted_when_configured()
    {
        var descriptor = CreateIssuer(includeFeatures: true)
            .Issue(CreateRequest(features: ["guides.browse", "messaging.send"]));

        Read(descriptor.Token).Claims
            .Where(c => c.Type == DiasporaClaimTypes.Feature)
            .Should().HaveCount(2);
    }

    [Fact]
    public void The_token_is_signed_with_hmac_sha256()
    {
        var descriptor = CreateIssuer().Issue(CreateRequest());

        Read(descriptor.Token).Alg.Should().Be("HS256");
    }

    [Fact]
    public void The_lifetime_matches_the_configured_window()
    {
        var descriptor = CreateIssuer().Issue(CreateRequest());

        descriptor.IssuedAt.Should().Be(Clock.UtcNow);
        descriptor.ExpiresAt.Should().Be(Clock.UtcNow.AddMinutes(15));
        descriptor.ExpiresInSeconds.Should().Be(900);
    }

    [Fact]
    public void Every_token_gets_a_distinct_identifier()
    {
        var issuer = CreateIssuer();

        var first = issuer.Issue(CreateRequest());
        var second = issuer.Issue(CreateRequest());

        first.TokenId.Should().NotBe(second.TokenId);
    }

    [Fact]
    public void The_issuer_and_audience_are_stamped_on_the_token()
    {
        var token = Read(CreateIssuer().Issue(CreateRequest()).Token);

        token.Issuer.Should().Be("https://identity.diaspora.test");
        token.Audiences.Should().Contain("https://api.diaspora.test");
    }

    [Fact]
    public void The_token_never_carries_the_password_hash_or_phone_number()
    {
        var token = Read(CreateIssuer().Issue(CreateRequest()).Token);
        var payload = string.Join(' ', token.Claims.Select(c => $"{c.Type}={c.Value}"));

        payload.Should().NotContain("hashed::");
        payload.Should().NotContain("+234");
    }
}
