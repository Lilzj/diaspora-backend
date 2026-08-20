using System.Security.Cryptography;
using System.Text;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Integrations.Domain.OAuth;

/// <summary>
/// One authorization attempt, from the redirect to the exchange.
/// <para>
/// <b>This row is what makes an OAuth callback verifiable, and every field on it is a control.</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="StateHash"/> — the callback's <c>state</c> is looked up here and nowhere else. A
/// callback carrying a state this platform never issued matches no row and is refused. "Never trust
/// <c>state</c> without validation" becomes a database lookup that can only succeed against
/// something the server minted.
/// </item>
/// <item>
/// <see cref="RedirectUri"/> — the URI *approved at authorization time*, not the one the client
/// sends at exchange time. The exchange compares and refuses a mismatch. Accepting the client's is
/// the canonical OAuth code-interception vulnerability, and the allow-list alone does not close it:
/// two URIs can both be allow-listed and still not be the same flow.
/// </item>
/// <item>
/// <see cref="CodeChallenge"/> — PKCE, mandatory. The client is public and cannot authenticate
/// itself; the challenge recorded here is what the verifier presented at exchange must hash to.
/// </item>
/// <item>
/// <see cref="ConsumedAt"/> — single use. "An authorization code is single-use by specification. A
/// replayed code MUST return 409, not a second connection." The code is the provider's to police;
/// this row is what lets the platform refuse the replay *before* spending a call on it, and refuse
/// it the same way whether or not the provider bothers.
/// </item>
/// </list>
/// <para>
/// <b>No secret is stored.</b> The state is held as a SHA-256 rather than in the clear, so a
/// database disclosure does not hand somebody a set of live state values to race the real member
/// with; the challenge is already a hash by construction; and the verifier never reaches the server
/// until the exchange, where it is used and discarded.
/// </para>
/// </summary>
public sealed class OAuthAuthorizationRequest : AggregateRoot<OAuthAuthorizationRequestId>
{
    /// <summary>Base64url of a SHA-256: 43 characters. Widened for a future algorithm.</summary>
    public const int HashLength = 64;

    /// <summary>A redirect URI. Generous, and capped — an unbounded URL is an unbounded column.</summary>
    public const int RedirectUriLength = 512;

    public const int ScopesLength = 2000;

    /// <summary>
    /// How long an authorization may stay open.
    /// <para>
    /// Ten minutes. Long enough for a member to read a consent screen and pick an account, short
    /// enough that an abandoned attempt is not a live state value sitting in a table overnight.
    /// Providers' own codes expire in the same order of magnitude.
    /// </para>
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private OAuthAuthorizationRequest(
        OAuthAuthorizationRequestId id,
        IntegrationOwnerKind ownerKind,
        Guid? ownerId,
        ProviderKey provider,
        string stateHash,
        string codeChallenge,
        string codeChallengeMethod,
        string redirectUri,
        OAuthIntent intent,
        DateTimeOffset createdAt) : base(id)
    {
        OwnerKind = ownerKind;
        OwnerId = ownerId;
        Provider = provider.Value;
        StateHash = stateHash;
        CodeChallenge = codeChallenge;
        CodeChallengeMethod = codeChallengeMethod;
        RedirectUri = redirectUri;
        Intent = intent;
        CreatedAt = createdAt;
        ExpiresAt = createdAt + Lifetime;
    }

    private OAuthAuthorizationRequest() { }

    public IntegrationOwnerKind OwnerKind { get; private set; }

    /// <summary>
    /// Who started it. Null only for an anonymous sign-in attempt.
    /// <para>
    /// Compared against the caller at exchange time, so a state value leaked to a second member
    /// cannot be redeemed by them — which would attach the first member's Google account to the
    /// second member's Diaspora account.
    /// </para>
    /// </summary>
    public Guid? OwnerId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    /// <summary>Base64url SHA-256 of the state value. Uniquely indexed.</summary>
    public string StateHash { get; private set; } = string.Empty;

    public string CodeChallenge { get; private set; } = string.Empty;

    /// <summary><c>S256</c>. <c>plain</c> is not accepted; see <see cref="Start"/>.</summary>
    public string CodeChallengeMethod { get; private set; } = string.Empty;

    public string RedirectUri { get; private set; } = string.Empty;

    public OAuthIntent Intent { get; private set; }

    private string? _scopes;

    /// <summary>The scopes asked for. Compared at exchange so a broader grant is visible.</summary>
    public IReadOnlyList<string> RequestedScopes =>
        string.IsNullOrWhiteSpace(_scopes)
            ? []
            : _scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When it was redeemed. Non-null means it can never be redeemed again.</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>Whether this request can still be redeemed at <paramref name="now"/>.</summary>
    public bool IsRedeemable(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Opens an authorization.
    /// <para>
    /// <b><c>S256</c> only.</b> RFC 7636 permits <c>plain</c>, and <c>plain</c> provides no
    /// protection whatsoever against an attacker who can observe the authorization request — which
    /// is the entire threat PKCE exists for. Accepting it because a provider allows it would make
    /// the mandatory control optional in practice.
    /// </para>
    /// </summary>
    public static OAuthAuthorizationRequest Start(
        OAuthAuthorizationRequestId id,
        IntegrationOwnerKind ownerKind,
        Guid? ownerId,
        ProviderKey provider,
        string state,
        string codeChallenge,
        string redirectUri,
        OAuthIntent intent,
        IReadOnlyList<string> scopes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeChallenge);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        ArgumentNullException.ThrowIfNull(scopes);

        return new OAuthAuthorizationRequest(
            id,
            ownerKind,
            ownerId,
            provider,
            HashState(state),
            codeChallenge.Trim(),
            "S256",
            redirectUri.Trim(),
            intent,
            now)
        {
            _scopes = string.Join(
                ' ',
                scopes.Where(scope => !string.IsNullOrWhiteSpace(scope))
                    .Select(scope => scope.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal))
        };
    }

    /// <summary>
    /// Marks it redeemed. Returns false when it already was, or has expired.
    /// <para>
    /// Returning rather than throwing, because both are ordinary answers a handler turns into a
    /// specific refusal — a replay is a 409 and an expiry is a 400, and a caller cannot tell them
    /// apart from an exception type.
    /// </para>
    /// </summary>
    public bool TryConsume(DateTimeOffset now)
    {
        if (!IsRedeemable(now))
        {
            return false;
        }

        ConsumedAt = now;

        return true;
    }

    /// <summary>
    /// Whether <paramref name="verifier"/> satisfies the recorded challenge.
    /// <para>
    /// RFC 7636 §4.6: base64url(SHA-256(ASCII(verifier))). Compared in fixed time — a challenge
    /// comparison that returns early leaks the challenge one byte at a time to anybody who can
    /// measure the response.
    /// </para>
    /// </summary>
    public bool MatchesVerifier(string? verifier)
    {
        if (string.IsNullOrWhiteSpace(verifier))
        {
            return false;
        }

        var computed = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier.Trim())));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(CodeChallenge));
    }

    /// <summary>The stored form of a state value. Also how a callback's state is looked up.</summary>
    public static string HashState(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        return Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(state.Trim())));
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
