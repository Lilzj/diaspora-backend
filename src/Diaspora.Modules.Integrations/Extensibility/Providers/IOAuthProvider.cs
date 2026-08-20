using Diaspora.Modules.Integrations.Domain.Connections;

namespace Diaspora.Modules.Integrations.Extensibility.Providers;

/// <summary>
/// An OAuth 2.0 authorization server, behind one interface.
/// <para>
/// <b>Registered per provider key.</b> The registry resolves by <c>Key</c>, so a deployment ships an
/// adapter for <c>identity.google</c> and one for <c>calendar.google</c> if it wants both — they are
/// different scopes on the same vendor and the register is explicit that conflating them is a
/// mistake. A deployment that ships neither gets
/// <see cref="Diaspora.Modules.Integrations.Infrastructure.Providers.NotConfiguredOAuthProvider"/>,
/// which says so.
/// </para>
/// <para>
/// <b>No method here takes or returns a Diaspora session.</b> Minting one is Identity's, and an
/// OAuth adapter that could produce one would be an authentication bypass reachable from whichever
/// vendor SDK a deployment happened to install.
/// </para>
/// <para>
/// <b>The adapter never persists anything.</b> It translates: a redirect into a URL, a code into a
/// grant, a refresh token into a new grant, a revocation into a boolean. Custody, replay protection
/// and the connection record are this module's, so a second adapter cannot get any of the three
/// subtly different.
/// </para>
/// </summary>
public interface IOAuthProvider
{
    /// <summary>The dotted provider key this serves — <c>identity.google</c>, <c>calendar.google</c>.</summary>
    string Key { get; }

    /// <summary>
    /// Whether this deployment actually has what the adapter needs.
    /// <para>
    /// A client id, a secret and an authorization endpoint. False is a first-class answer that every
    /// route branches on, and it is why a deployment with no credentials starts rather than crashing.
    /// </para>
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// The scopes this adapter will ask for, given what the caller wants.
    /// <para>
    /// The adapter narrows: "a member connecting a calendar must not be asked for contacts". A
    /// requested scope the adapter does not recognise is dropped rather than forwarded, so a client
    /// cannot widen a grant by adding a string to an array.
    /// </para>
    /// </summary>
    IReadOnlyList<string> NarrowScopes(IReadOnlyList<string> requested);

    /// <summary>
    /// Where to send the member.
    /// <para>
    /// The adapter composes the URL; the state and the challenge are given to it, because both are
    /// controls this module owns and an adapter that minted its own would be an adapter that could
    /// mint a weak one.
    /// </para>
    /// </summary>
    IntegrationOutcome<Uri> BuildAuthorizationUri(OAuthAuthorizationParameters parameters);

    /// <summary>
    /// Exchanges an authorization code for a grant.
    /// <para>
    /// A replayed code is the provider's refusal to honour, and it arrives here as
    /// <see cref="Diaspora.Modules.Integrations.Domain.Enums.IntegrationFailureKind.InvalidRequest"/>.
    /// This module refuses the replay before the call as well, on its own record — two barriers,
    /// because only one of them is under this platform's control.
    /// </para>
    /// </summary>
    Task<IntegrationOutcome<OAuthGrant>> ExchangeAsync(
        OAuthExchangeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trades a refresh token for a new access token.
    /// <para>
    /// A provider that refuses returns
    /// <see cref="Diaspora.Modules.Integrations.Domain.Enums.IntegrationFailureKind.Unauthorized"/>,
    /// which is what moves the connection to <c>REAUTH_REQUIRED</c> and drops the ciphers. A refused
    /// refresh mapped to <c>Unavailable</c> would leave a dead grant looking retryable for ever.
    /// </para>
    /// </summary>
    Task<IntegrationOutcome<OAuthGrant>> RefreshAsync(
        string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the provider to drop the grant.
    /// <para>
    /// Returns whether the provider confirmed it. <b>A failure here must not block the local
    /// disconnect</b>: a member who asked to disconnect has disconnected, and a provider outage must
    /// not leave the platform holding a token it was told to forget. The boolean is reported to the
    /// client as <c>revokedAtProvider</c> so the difference is visible rather than assumed.
    /// </para>
    /// </summary>
    Task<IntegrationOutcome<bool>> RevokeAsync(
        OAuthRevocationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>What an authorization URL is built from.</summary>
/// <param name="RedirectUri">Already validated against this deployment's allow-list.</param>
/// <param name="State">Opaque, single-use, and stored only as a hash.</param>
/// <param name="CodeChallenge">Base64url SHA-256 of the client's verifier. PKCE is mandatory.</param>
/// <param name="Scopes">Already narrowed by <see cref="IOAuthProvider.NarrowScopes"/>.</param>
/// <param name="LoginHint">
/// An account the member already named, so a provider can skip the picker. Optional, and never
/// populated from anything the member did not type — a hint derived from a stored address would
/// disclose it to the provider before consent.
/// </param>
public sealed record OAuthAuthorizationParameters(
    string RedirectUri,
    string State,
    string CodeChallenge,
    IReadOnlyList<string> Scopes,
    string? LoginHint = null);

/// <summary>An authorization code, and the two things that prove it belongs to this flow.</summary>
/// <param name="Code">Single-use by specification.</param>
/// <param name="CodeVerifier">The PKCE verifier. Already checked against the recorded challenge.</param>
/// <param name="RedirectUri">
/// The URI recorded at authorization time, not the one the client sent at exchange. See
/// <see cref="Diaspora.Modules.Integrations.Domain.OAuth.OAuthAuthorizationRequest"/>.
/// </param>
public sealed record OAuthExchangeRequest(string Code, string CodeVerifier, string RedirectUri);

/// <summary>What to revoke, and with what.</summary>
/// <param name="AccessToken">The current access token, if one is still held.</param>
/// <param name="RefreshToken">
/// The refresh token. Preferred where a provider accepts either: revoking the refresh token drops
/// the whole grant, where revoking an access token drops one hour of it.
/// </param>
public sealed record OAuthRevocationRequest(string? AccessToken, string? RefreshToken);

/// <summary>
/// A grant, as the provider issued it.
/// <para>
/// The plaintext tokens exist on this record and nowhere else for the length of one handler call:
/// the caller seals them immediately and stores ciphertext. This type is never serialised, never
/// logged and never returned from a route.
/// </para>
/// </summary>
/// <param name="AccessToken">Plaintext, briefly.</param>
/// <param name="RefreshToken">Plaintext, briefly. Null when the provider issued none.</param>
/// <param name="ExpiresAt">When the access token stops working, if the provider said.</param>
/// <param name="GrantedScopes">
/// What the provider actually granted, which is not always what was asked for. Recorded as granted
/// so the centre shows the truth rather than the request.
/// </param>
/// <param name="Account">Who the grant is for.</param>
public sealed record OAuthGrant(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<string> GrantedScopes,
    ExternalAccount Account);
