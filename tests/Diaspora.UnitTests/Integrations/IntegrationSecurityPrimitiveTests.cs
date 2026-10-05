using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.OAuth;
using Diaspora.Modules.Integrations.Domain.Providers;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Integrations;

/// <summary>
/// The four primitives an integration boundary's security rests on, tested on their own.
/// <para>
/// <b>These are the places a defect would be invisible at the wire.</b> A provider key that admitted
/// a path separator, a state lookup that matched a value nobody issued, a PKCE comparison that
/// returned early, or an authorization request that could be redeemed twice — every one of them
/// produces a system that works perfectly in every test that is not looking for it.
/// </para>
/// </summary>
public sealed class IntegrationSecurityPrimitiveTests
{
    // ── ProviderKey ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The dotted form parses into its two halves, and the domain is not discarded.
    /// <para>
    /// The frontend's own reason: "<c>calendar.apple</c> and <c>identity.apple</c> are different
    /// integrations that happen to share a vendor". A parser that kept only the vendor would make
    /// one grant serve both, which is a scope escalation with a friendly name.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("calendar.google", "calendar", "google")]
    [InlineData("calendar.apple", "calendar", "apple")]
    [InlineData("CALENDAR.GOOGLE", "calendar", "google")]
    [InlineData("  calendar.device  ", "calendar", "device")]
    public void A_dotted_provider_key_parses_into_domain_and_vendor(
        string input, string domain, string vendor)
    {
        ProviderKey.TryParse(input, out var key).Should().BeTrue();

        key.Domain.Should().Be(domain);
        key.Provider.Should().Be(vendor);
        key.Value.Should().Be($"{domain}.{vendor}");
    }

    /// <summary>
    /// Everything a URL segment could smuggle is refused.
    /// <para>
    /// A provider key arrives as a path segment, so it is attacker-controlled. Each of these would
    /// otherwise reach a database column, a log line, or — for the traversal cases — a comparison
    /// against a registered adapter key.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("google")]                     // no domain
    [InlineData("calendar.")]                  // no vendor
    [InlineData(".google")]                    // no domain
    [InlineData("calendar.google.extra")]      // ambiguous: two readings, neither safe to pick
    [InlineData("calendar/../identity.google")]
    [InlineData("calendar.goo gle")]
    [InlineData("calendar.google\0")]
    [InlineData("calendar.google%2f")]
    [InlineData("identity.google")]            // sign-in identities were removed on 5 Oct 2026
    [InlineData("identity.apple")]
    public void A_key_that_is_not_exactly_two_safe_segments_is_refused(string? input)
    {
        ProviderKey.TryParse(input, out _).Should().BeFalse();
    }

    /// <summary>A key longer than the column is refused rather than silently truncated.</summary>
    [Fact]
    public void An_over_long_key_is_refused_rather_than_truncated()
    {
        var oversized = "calendar." + new string('a', ProviderKey.MaxLength);

        ProviderKey.TryParse(oversized, out _).Should().BeFalse();
    }

    // ── OAuth state ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The state is stored as a hash, and the hash is not the state.
    /// <para>
    /// A database disclosure must not hand somebody a set of live state values to race the real
    /// member with. The lookup hashes on the way in, so this costs nothing at the call site.
    /// </para>
    /// </summary>
    [Fact]
    public void The_state_value_is_stored_as_a_hash_and_not_in_the_clear()
    {
        const string state = "a-state-value-the-server-minted";

        var request = StartRequest(state: state);

        request.StateHash.Should().NotBe(state);
        request.StateHash.Should().Be(OAuthAuthorizationRequest.HashState(state));

        // Base64url: no padding, no '+', no '/'. It travels in a query string.
        request.StateHash.Should().NotContain("=").And.NotContain("+").And.NotContain("/");
    }

    /// <summary>
    /// PKCE is verified against the recorded challenge, and only the right verifier satisfies it.
    /// </summary>
    [Fact]
    public void Only_the_verifier_that_produced_the_challenge_satisfies_it()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

        var request = StartRequest(challenge: Challenge(verifier));

        request.MatchesVerifier(verifier).Should().BeTrue();
        request.MatchesVerifier(verifier + "x").Should().BeFalse();
        request.MatchesVerifier("something-else-entirely").Should().BeFalse();
        request.MatchesVerifier(null).Should().BeFalse();
        request.MatchesVerifier("   ").Should().BeFalse();
    }

    /// <summary>
    /// <c>plain</c> is never recorded, whatever a provider permits.
    /// <para>
    /// RFC 7636 allows it and it provides no protection against an attacker who can observe the
    /// authorization request — which is the entire threat PKCE exists for.
    /// </para>
    /// </summary>
    [Fact]
    public void The_challenge_method_is_always_S256()
    {
        StartRequest().CodeChallengeMethod.Should().Be("S256");
    }

    /// <summary>
    /// An authorization request is redeemable exactly once.
    /// <para>
    /// "An authorization code is single-use by specification. A replayed code MUST return 409, not a
    /// second connection." This is the record that lets the platform refuse the replay before
    /// spending a provider call on it — and refuse it the same way whether or not the provider
    /// bothers.
    /// </para>
    /// </summary>
    [Fact]
    public void An_authorization_request_can_be_redeemed_once_and_never_again()
    {
        var now = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var request = StartRequest(now: now);

        request.IsRedeemable(now).Should().BeTrue();
        request.TryConsume(now).Should().BeTrue();

        request.ConsumedAt.Should().Be(now);
        request.IsRedeemable(now).Should().BeFalse();
        request.TryConsume(now).Should().BeFalse();
        request.TryConsume(now.AddSeconds(1)).Should().BeFalse();
    }

    /// <summary>An abandoned authorization stops being redeemable on its own.</summary>
    [Fact]
    public void An_expired_authorization_request_is_not_redeemable()
    {
        var now = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var request = StartRequest(now: now);

        request.ExpiresAt.Should().Be(now + OAuthAuthorizationRequest.Lifetime);

        var afterExpiry = now + OAuthAuthorizationRequest.Lifetime;

        request.IsExpired(afterExpiry).Should().BeTrue();
        request.IsRedeemable(afterExpiry).Should().BeFalse();
        request.TryConsume(afterExpiry).Should().BeFalse();
        request.ConsumedAt.Should().BeNull("an expired request must not be marked as redeemed");
    }

    /// <summary>
    /// The redirect recorded is the one that was approved, not one supplied later.
    /// <para>
    /// The exchange compares the client's against this. Accepting the client's instead is the
    /// canonical OAuth code-interception vulnerability.
    /// </para>
    /// </summary>
    [Fact]
    public void The_approved_redirect_is_recorded_on_the_request()
    {
        const string approved = "https://app.diaspora.test/oauth/callback";

        StartRequest(redirectUri: approved).RedirectUri.Should().Be(approved);
    }

    /// <summary>Scopes are recorded ordered and deduplicated, so two grants compare cleanly.</summary>
    [Fact]
    public void Requested_scopes_are_normalised()
    {
        var request = StartRequest(scopes: ["calendar.write", "calendar.read", "calendar.read", "  "]);

        request.RequestedScopes.Should().Equal("calendar.read", "calendar.write");
    }

    // ── Credential custody ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// A connection exposes no token, and the sealed pair is only reachable through one method.
    /// <para>
    /// The frontend enforced this by having nowhere to put a token. This is the same enforcement on
    /// the side that actually holds them: reflection over the public surface finds no property whose
    /// name suggests one.
    /// </para>
    /// </summary>
    [Fact]
    public void No_public_property_on_a_connection_exposes_a_token()
    {
        var offenders = typeof(IntegrationConnection)
            .GetProperties()
            .Where(property =>
                property.PropertyType == typeof(string)
                && (property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("Cipher", StringComparison.OrdinalIgnoreCase)))
            .Select(property => property.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "a token reachable from a public property is a token a mapper will one day serialise");
    }

    /// <summary>
    /// A grant makes a connection usable, and the ciphers are what it holds — never plaintext.
    /// </summary>
    [Fact]
    public void Applying_a_grant_connects_and_stores_only_ciphertext()
    {
        var now = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var connection = NewConnection(now);

        connection.Status.Should().Be(IntegrationConnectionStatus.Disconnected);
        connection.IsUsable.Should().BeFalse();

        connection.ApplyGrant(
            new ExternalAccount("google-account-1", "amara@example.test"),
            ["calendar.read", "calendar.write"],
            "cipher:access",
            "cipher:refresh",
            "key-v1",
            now.AddHours(1),
            now);

        connection.Status.Should().Be(IntegrationConnectionStatus.Connected);
        connection.IsUsable.Should().BeTrue();
        connection.HasRefreshToken.Should().BeTrue();
        connection.ConnectedAt.Should().Be(now);

        var sealedCredential = connection.ReadCredential();
        sealedCredential.Should().NotBeNull();
        sealedCredential!.AccessTokenCipher.Should().Be("cipher:access");
        sealedCredential.RefreshTokenCipher.Should().Be("cipher:refresh");
        sealedCredential.KeyId.Should().Be("key-v1");
    }

    /// <summary>
    /// A re-authorization that returns no refresh token keeps the one already held.
    /// <para>
    /// Google issues a refresh token once. Dropping it on the second authorization would silently
    /// downgrade a connection to access-token-only, and it would break at the next expiry with no
    /// visible cause.
    /// </para>
    /// </summary>
    [Fact]
    public void A_reauthorization_without_a_refresh_token_keeps_the_existing_one()
    {
        var now = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var connection = Connected(now);

        connection.ApplyGrant(
            new ExternalAccount("google-account-1", "amara@example.test"),
            ["calendar.read"],
            "cipher:access-2",
            refreshTokenCipher: null,
            "key-v1",
            now.AddHours(1),
            now.AddMinutes(5));

        connection.HasRefreshToken.Should().BeTrue();
        connection.ReadCredential()!.RefreshTokenCipher.Should().Be("cipher:refresh");
    }

    /// <summary>
    /// An authorization failure ends the grant and drops both ciphers.
    /// <para>
    /// A refresh token the provider has already rejected is a credential with no use and a liability
    /// with a long life. Keeping it means a database disclosure hands over tokens for accounts that
    /// were already detached at the provider.
    /// </para>
    /// </summary>
    [Fact]
    public void An_authorization_failure_ends_the_grant_and_drops_the_ciphers()
    {
        var now = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var connection = Connected(now);

        connection.RecordFailure(
            IntegrationFailureKind.Unauthorized, "The provider refused the credential.", now);

        connection.Status.Should().Be(IntegrationConnectionStatus.ReauthorizationRequired);
        connection.IsUsable.Should().BeFalse();
        connection.HasRefreshToken.Should().BeFalse();
        connection.ReadCredential().Should().BeNull();
        connection.ExpiresAt.Should().BeNull();
    }

    /// <summary>
    /// Any other failure leaves the grant usable, because the recovery is a retry rather than a
    /// reconnection.
    /// </summary>
    [Theory]
    [InlineData(IntegrationFailureKind.Unavailable)]
    [InlineData(IntegrationFailureKind.RateLimited)]
    [InlineData(IntegrationFailureKind.ProviderError)]
    public void A_transient_failure_leaves_the_grant_usable(IntegrationFailureKind kind)
    {
        var now = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var connection = Connected(now);

        connection.RecordFailure(kind, "The provider did not answer.", now);

        connection.Status.Should().Be(IntegrationConnectionStatus.Error);
        connection.IsUsable.Should().BeTrue("a provider outage is not a reason to make somebody reconnect");
        connection.LastFailure!.Kind.Should().Be(kind);
    }

    /// <summary>
    /// Disconnecting twice produces the same state and does not move the moment it happened.
    /// <para>
    /// The register: "Disconnecting an already-disconnected integration returns 200 with the same
    /// body, never 404." The aggregate is where that has to be true first.
    /// </para>
    /// </summary>
    [Fact]
    public void Disconnecting_is_idempotent_and_preserves_the_original_moment()
    {
        var now = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var connection = Connected(now);

        connection.Disconnect(revokedAtProvider: true, now);

        var firstRevokedAt = connection.RevokedAt;

        connection.Disconnect(revokedAtProvider: true, now.AddHours(3));

        connection.Status.Should().Be(IntegrationConnectionStatus.Disconnected);
        connection.RevokedAt.Should().Be(firstRevokedAt);
        connection.ReadCredential().Should().BeNull();
    }

    /// <summary>
    /// A retry that finally reaches the provider records the revocation the first attempt could not.
    /// </summary>
    [Fact]
    public void A_later_successful_revocation_is_recorded_on_an_already_disconnected_row()
    {
        var now = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var connection = Connected(now);

        connection.Disconnect(revokedAtProvider: false, now);
        connection.RevokedAtProvider.Should().BeFalse();

        connection.Disconnect(revokedAtProvider: true, now.AddMinutes(1));
        connection.RevokedAtProvider.Should().BeTrue();
    }

    /// <summary>
    /// A token about to expire is refreshed before the call, not after the 401.
    /// <para>
    /// Without the skew, a token expiring during the call it was checked for produces a 401
    /// indistinguishable from a revoked grant — which would ask a member to re-authorize something
    /// that was fine.
    /// </para>
    /// </summary>
    [Fact]
    public void A_token_inside_the_refresh_skew_is_due_for_refresh()
    {
        var now = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var connection = NewConnection(now);

        connection.ApplyGrant(
            new ExternalAccount("a", "a@example.test"),
            [],
            "cipher:access",
            "cipher:refresh",
            "key-v1",
            now.AddSeconds(30),
            now);

        connection.NeedsRefresh(now, TimeSpan.FromSeconds(120)).Should().BeTrue();
        connection.NeedsRefresh(now, TimeSpan.FromSeconds(10)).Should().BeFalse();
    }

    /// <summary>Scopes round-trip through the delimited column ordered and deduplicated.</summary>
    [Fact]
    public void Granted_scopes_are_normalised_and_never_contain_the_separator()
    {
        var now = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var connection = NewConnection(now);

        connection.ApplyGrant(
            new ExternalAccount("a", "a@example.test"),
            ["b.scope", "a.scope", "a.scope", "has space", ""],
            "cipher:access",
            null,
            "key-v1",
            null,
            now);

        connection.Scopes.Should().Equal("a.scope", "b.scope");
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private static OAuthAuthorizationRequest StartRequest(
        string state = "state-value",
        string? challenge = null,
        string redirectUri = "https://app.diaspora.test/oauth/callback",
        IReadOnlyList<string>? scopes = null,
        DateTimeOffset? now = null) =>
        OAuthAuthorizationRequest.Start(
            OAuthAuthorizationRequestId.New(),
            IntegrationOwnerKind.User,
            Guid.NewGuid(),
            ProviderKey.Parse("calendar.google"),
            state,
            challenge ?? Challenge("a-verifier-long-enough-to-be-realistic"),
            redirectUri,
            OAuthIntent.Link,
            scopes ?? [],
            now ?? DateTimeOffset.UnixEpoch);

    private static string Challenge(string verifier) =>
        Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static IntegrationConnection NewConnection(DateTimeOffset now) =>
        IntegrationConnection.Start(
            IntegrationConnectionId.New(),
            IntegrationOwnerKind.User,
            Guid.NewGuid(),
            ProviderKey.Parse("calendar.google"),
            now);

    private static IntegrationConnection Connected(DateTimeOffset now)
    {
        var connection = NewConnection(now);

        connection.ApplyGrant(
            new ExternalAccount("google-account-1", "amara@example.test"),
            ["calendar.read", "calendar.write"],
            "cipher:access",
            "cipher:refresh",
            "key-v1",
            now.AddHours(1),
            now);

        return connection;
    }
}
