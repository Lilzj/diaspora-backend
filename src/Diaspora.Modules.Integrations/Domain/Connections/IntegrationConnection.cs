using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Integrations.Domain.Connections;

/// <summary>
/// One owner's grant on one external provider.
/// <para>
/// <b>The credential lives here and leaves nowhere.</b> Two cipher columns, a protector key
/// identifier and an expiry; no property returns a plaintext token, no mapper reads one, and the
/// contract types this module publishes have no field one could be assigned to. That is the same
/// enforcement the frontend chose for the same reason — "a domain model with nowhere to put one" —
/// applied on the side that actually holds them.
/// </para>
/// <para>
/// <b>A disconnection is a state, not a delete.</b> The row survives with
/// <see cref="Enums.IntegrationConnectionStatus.Disconnected"/>, no cipher and a
/// <see cref="RevokedAt"/>, so a support conversation about "I disconnected Google last Tuesday" has
/// something to read and a reconnection is a new grant rather than a resurrection of an old one.
/// Deleting would also lose the fact that a token *was* held, which is the audit trail §34 asks for.
/// </para>
/// <para>
/// <b>The failure is four flat columns rather than an owned type.</b> Every column is nullable, and
/// an optional owned entity whose properties are all nullable is the shape EF Core models worst —
/// this platform has paid for that twice, once with owned-collection shadow keys and once with a
/// cached owned instance NULLing a second owner's columns. There is no behaviour on a failure
/// summary, so there is nothing an owned type would buy. <see cref="LastFailure"/> composes the
/// value object at read time from columns that cannot be shared.
/// </para>
/// </summary>
public sealed class IntegrationConnection : AggregateRoot<IntegrationConnectionId>
{
    /// <summary>A provider's own account identifier. Opaque, and never treated as authorization.</summary>
    public const int ExternalAccountIdLength = 256;

    /// <summary>What the member recognises: an address, a calendar name.</summary>
    public const int AccountLabelLength = 256;

    /// <summary>
    /// The joined scope list.
    /// <para>
    /// A single delimited column rather than a child table. A scope list is read only with its
    /// connection, is never queried across connections, and is small — the three properties an owned
    /// collection would have bought are all unused, and every one of this platform's owned-collection
    /// defects would have been reachable from here.
    /// </para>
    /// </summary>
    public const int ScopesLength = 2000;

    /// <summary>Ciphertext. Sized for a protected JWT plus the protector's own envelope.</summary>
    public const int CipherLength = 4000;

    public const int FailureMessageLength = 500;

    private const char ScopeSeparator = ' ';

    private IntegrationConnection(
        IntegrationConnectionId id,
        IntegrationOwnerKind ownerKind,
        Guid ownerId,
        ProviderKey provider,
        DateTimeOffset createdAt) : base(id)
    {
        OwnerKind = ownerKind;
        OwnerId = ownerId;
        Provider = provider.Value;
        ProviderDomain = provider.Domain;
        Status = IntegrationConnectionStatus.Disconnected;
        CreatedAt = createdAt;
        ObservedAt = createdAt;
    }

    private IntegrationConnection() { }

    // ── Ownership ───────────────────────────────────────────────────────────────────────────

    public IntegrationOwnerKind OwnerKind { get; private set; }

    /// <summary>
    /// The owner's identifier, interpreted by <see cref="OwnerKind"/>.
    /// <para>
    /// Not a <c>UserId</c>, because it is not always one. Every repository method takes the pair and
    /// no method takes the identifier alone, which is what stops a community connection ever being
    /// returned to the member whose user identifier happens to collide with a community's.
    /// </para>
    /// </summary>
    public Guid OwnerId { get; private set; }

    // ── Identity ────────────────────────────────────────────────────────────────────────────

    /// <summary>The dotted provider key. Unique with the owner pair.</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>The domain half, denormalised so the centre can filter without parsing every row.</summary>
    public string ProviderDomain { get; private set; } = string.Empty;

    public IntegrationConnectionStatus Status { get; private set; }

    public string? ExternalAccountId { get; private set; }

    public string? AccountLabel { get; private set; }

    /// <summary>
    /// Apple's private-relay addresses are deliverable and are not the member's own.
    /// <para>
    /// Recorded because the centre must be able to say so rather than presenting
    /// <c>@privaterelay.appleid.com</c> as somebody's email address.
    /// </para>
    /// </summary>
    public bool IsPrivateRelay { get; private set; }

    public string? AvatarUrl { get; private set; }

    // ── The grant ───────────────────────────────────────────────────────────────────────────

    private string? _scopes;

    /// <summary>The scopes the provider actually granted. Names only.</summary>
    public IReadOnlyList<string> Scopes =>
        string.IsNullOrWhiteSpace(_scopes)
            ? []
            : _scopes.Split(ScopeSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public DateTimeOffset? ConnectedAt { get; private set; }

    /// <summary>When the access token stops working, if the provider said.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Whether the provider-side grant was actually revoked, as opposed to only forgotten.</summary>
    public bool RevokedAtProvider { get; private set; }

    /// <summary>
    /// The last time a call made with this grant succeeded.
    /// <para>
    /// Null means it has never been used, which is a different fact from "used and found nothing" —
    /// the register's own distinction, and the reason the client's <c>lastSyncedAt</c> is nullable.
    /// </para>
    /// </summary>
    public DateTimeOffset? LastSuccessAt { get; private set; }

    // ── The last failure, flat. See the class summary. ──────────────────────────────────────

    public IntegrationFailureKind? LastFailureKind { get; private set; }

    /// <summary>Member-facing, already in plain language. Never a provider payload, never a token.</summary>
    public string? LastFailureMessage { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }

    /// <summary>For a support conversation. The platform's correlation id, not the provider's.</summary>
    public string? LastFailureCorrelationId { get; private set; }

    /// <summary>Composed at read time. Null when nothing has failed since the last success.</summary>
    public ConnectionFailure? LastFailure =>
        LastFailureKind is { } kind && LastFailureAt is { } at
            ? new ConnectionFailure(kind, LastFailureMessage ?? string.Empty, at, LastFailureCorrelationId)
            : null;

    // ── Custody ─────────────────────────────────────────────────────────────────────────────
    //
    // Private setters and no public getter for either cipher. The only way a token leaves this
    // aggregate is `ReadCredential`, which returns the pair to a caller that already holds the
    // protector — and the protector is the thing that can actually read them.

    private string? _accessTokenCipher;
    private string? _refreshTokenCipher;

    /// <summary>
    /// Which protector version sealed the ciphers.
    /// <para>
    /// Stored so a key rotation is a re-seal of the rows that name the old version rather than a
    /// re-authorization of every member. Without it, rotating the protection key is indistinguishable
    /// from losing every grant on the platform.
    /// </para>
    /// </summary>
    public string? CredentialKeyId { get; private set; }

    /// <summary>Whether a refresh token is held. Reported; never the token.</summary>
    public bool HasRefreshToken => !string.IsNullOrEmpty(_refreshTokenCipher);

    // ── Bookkeeping ─────────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When this row last changed. Served as the client's <c>observedAt</c>.</summary>
    public DateTimeOffset ObservedAt { get; private set; }

    // ── Construction ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A connection row that exists and holds nothing.
    /// <para>
    /// Created by the exchange before the grant is applied, so that a failure between the two leaves
    /// a disconnected row rather than an orphaned token. Also what a disconnect leaves behind.
    /// </para>
    /// </summary>
    public static IntegrationConnection Start(
        IntegrationConnectionId id,
        IntegrationOwnerKind ownerKind,
        Guid ownerId,
        ProviderKey provider,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentOutOfRangeException.ThrowIfEqual(ownerId, Guid.Empty);

        return new IntegrationConnection(id, ownerKind, ownerId, provider, now);
    }

    // ── Transitions ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Records a grant the provider issued.
    /// <para>
    /// Takes ciphertext, not tokens. The aggregate cannot encrypt — that needs a protector, which
    /// needs configuration, which is infrastructure — and taking plaintext "just to encrypt it here"
    /// would put a token in a domain object's memory and, one careless <c>ToString</c> later, in a
    /// log.
    /// </para>
    /// </summary>
    public void ApplyGrant(
        ExternalAccount account,
        IReadOnlyList<string> scopes,
        string accessTokenCipher,
        string? refreshTokenCipher,
        string credentialKeyId,
        DateTimeOffset? expiresAt,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessTokenCipher);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialKeyId);

        ExternalAccountId = Truncate(account.ExternalId, ExternalAccountIdLength);
        AccountLabel = Truncate(account.Label, AccountLabelLength);
        IsPrivateRelay = account.IsPrivateRelay;
        AvatarUrl = Truncate(account.AvatarUrl, AccountLabelLength);

        _scopes = JoinScopes(scopes);
        _accessTokenCipher = accessTokenCipher;

        // A provider that returns no refresh token on a re-authorization has not withdrawn the one
        // it issued the first time — Google in particular issues it once. Keeping the existing
        // cipher is what stops a re-authorization silently downgrading a connection to
        // access-token-only, which then breaks at the next expiry with no visible cause.
        if (!string.IsNullOrWhiteSpace(refreshTokenCipher))
        {
            _refreshTokenCipher = refreshTokenCipher;
        }

        CredentialKeyId = credentialKeyId;
        ExpiresAt = expiresAt;
        Status = IntegrationConnectionStatus.Connected;
        ConnectedAt ??= now;
        RevokedAt = null;
        RevokedAtProvider = false;
        ClearFailure();
        Touch(now);
    }

    /// <summary>Replaces the access token after a refresh. Leaves the account and scopes alone.</summary>
    public void ApplyRefreshedAccessToken(
        string accessTokenCipher,
        string? refreshTokenCipher,
        string credentialKeyId,
        DateTimeOffset? expiresAt,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessTokenCipher);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialKeyId);

        _accessTokenCipher = accessTokenCipher;

        if (!string.IsNullOrWhiteSpace(refreshTokenCipher))
        {
            _refreshTokenCipher = refreshTokenCipher;
        }

        CredentialKeyId = credentialKeyId;
        ExpiresAt = expiresAt;
        Status = IntegrationConnectionStatus.Connected;
        ClearFailure();
        Touch(now);
    }

    /// <summary>
    /// The grant no longer works and only the member can fix it.
    /// <para>
    /// <b>The ciphers are dropped.</b> A refresh token the provider has already rejected is a
    /// credential with no use and a liability with a long life, and keeping it would mean a database
    /// disclosure hands over tokens for accounts that had already been disconnected at the provider.
    /// </para>
    /// </summary>
    public void RequireReauthorization(string reason, DateTimeOffset now, string? correlationId = null)
    {
        Status = IntegrationConnectionStatus.ReauthorizationRequired;
        _accessTokenCipher = null;
        _refreshTokenCipher = null;
        ExpiresAt = null;
        RecordFailure(IntegrationFailureKind.Unauthorized, reason, now, correlationId);
        Touch(now);
    }

    /// <summary>
    /// The member disconnected, or a provider signal said the grant is gone.
    /// <para>
    /// <b>Idempotent, and it has to be.</b> The register requires that disconnecting an
    /// already-disconnected integration answers 200 with the same body — "a 404 on the second call is
    /// exactly how a retry leaves [the UI] saying Connected". Calling this twice produces the same
    /// state and the second call does not move <see cref="RevokedAt"/>, so the audit trail keeps the
    /// moment it actually happened.
    /// </para>
    /// </summary>
    public void Disconnect(bool revokedAtProvider, DateTimeOffset now)
    {
        if (Status is IntegrationConnectionStatus.Disconnected)
        {
            // Still records a successful provider-side revocation that the first attempt could not
            // achieve — a retry after the provider was down is the reason the flag exists.
            RevokedAtProvider = RevokedAtProvider || revokedAtProvider;

            return;
        }

        Status = IntegrationConnectionStatus.Disconnected;
        _accessTokenCipher = null;
        _refreshTokenCipher = null;
        CredentialKeyId = null;
        ExpiresAt = null;
        RevokedAt = now;
        RevokedAtProvider = revokedAtProvider;
        ClearFailure();
        Touch(now);
    }

    /// <summary>A call through this grant worked.</summary>
    public void RecordSuccess(DateTimeOffset now)
    {
        LastSuccessAt = now;

        if (Status is IntegrationConnectionStatus.Error)
        {
            Status = IntegrationConnectionStatus.Connected;
        }

        ClearFailure();
        Touch(now);
    }

    /// <summary>
    /// A call through this grant failed.
    /// <para>
    /// <b>An authorization failure is not an error state.</b> It moves the connection to
    /// <see cref="IntegrationConnectionStatus.ReauthorizationRequired"/>, because the recovery is
    /// different and a member offered "retry" for a revoked grant will retry forever. Everything else
    /// leaves the grant usable and records why the last attempt did not work.
    /// </para>
    /// </summary>
    public void RecordFailure(
        IntegrationFailureKind kind, string message, DateTimeOffset now, string? correlationId = null)
    {
        LastFailureKind = kind;
        LastFailureMessage = Truncate(message, FailureMessageLength);
        LastFailureAt = now;
        LastFailureCorrelationId = Truncate(correlationId, 128);

        if (kind is IntegrationFailureKind.Unauthorized
            && Status is IntegrationConnectionStatus.Connected)
        {
            Status = IntegrationConnectionStatus.ReauthorizationRequired;
            _accessTokenCipher = null;
            _refreshTokenCipher = null;
            ExpiresAt = null;
        }
        else if (Status is IntegrationConnectionStatus.Connected
                 && kind is not IntegrationFailureKind.NotConfigured
                     and not IntegrationFailureKind.Unsupported)
        {
            Status = IntegrationConnectionStatus.Error;
        }

        Touch(now);
    }

    /// <summary>Whether a call may be attempted through this grant right now.</summary>
    public bool IsUsable =>
        Status is IntegrationConnectionStatus.Connected or IntegrationConnectionStatus.Error
        && !string.IsNullOrEmpty(_accessTokenCipher);

    /// <summary>
    /// Whether the access token should be refreshed before the next call.
    /// <para>
    /// A skew rather than a strict comparison: a token that expires in four seconds will expire
    /// during the call it was checked for, and the resulting 401 is indistinguishable from a revoked
    /// grant — which would move a working connection to <c>REAUTH_REQUIRED</c> and ask a member to
    /// re-authorize something that was fine.
    /// </para>
    /// </summary>
    public bool NeedsRefresh(DateTimeOffset now, TimeSpan skew) =>
        ExpiresAt is { } expiry && now + skew >= expiry;

    /// <summary>
    /// The sealed credential, for a caller holding the protector.
    /// <para>
    /// Returns ciphertext. The protector is the only thing that can open it, the protector lives in
    /// infrastructure, and the separation is what keeps a plaintext token out of every layer that
    /// does not need one.
    /// </para>
    /// </summary>
    public SealedCredential? ReadCredential() =>
        string.IsNullOrEmpty(_accessTokenCipher) || CredentialKeyId is null
            ? null
            : new SealedCredential(_accessTokenCipher, _refreshTokenCipher, CredentialKeyId, ExpiresAt);

    private void ClearFailure()
    {
        LastFailureKind = null;
        LastFailureMessage = null;
        LastFailureAt = null;
        LastFailureCorrelationId = null;
    }

    private void Touch(DateTimeOffset now) => ObservedAt = now;

    /// <summary>
    /// Scopes, joined by a space and deduplicated.
    /// <para>
    /// Space is the separator OAuth 2.0 itself uses (RFC 6749 §3.3), so a scope value cannot contain
    /// one and the round trip is lossless. Ordered, so two grants of the same scopes in a different
    /// order compare equal in a diff.
    /// </para>
    /// </summary>
    private static string JoinScopes(IReadOnlyList<string> scopes)
    {
        var cleaned = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Where(scope => !scope.Contains(ScopeSeparator, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var joined = string.Join(ScopeSeparator, cleaned);

        return joined.Length <= ScopesLength ? joined : joined[..ScopesLength];
    }

    private static string? Truncate(string? value, int length) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Length <= length ? value.Trim() : value.Trim()[..length];
}

/// <summary>
/// The account a grant belongs to, as much of it as is safe to hold.
/// <para>
/// A plain record rather than an owned entity: it is assigned as a unit and never queried, and this
/// platform has a standing defect where handing one owned instance to a second aggregate re-parents
/// it and NULLs the first owner's columns. Nothing here can be shared because nothing here is
/// persisted as an object.
/// </para>
/// </summary>
public sealed record ExternalAccount(
    string ExternalId,
    string Label,
    bool IsPrivateRelay = false,
    string? AvatarUrl = null);

/// <summary>The renderable part of a failure. No stack, no provider payload, no token.</summary>
public sealed record ConnectionFailure(
    IntegrationFailureKind Kind,
    string Message,
    DateTimeOffset OccurredAt,
    string? CorrelationId);

/// <summary>A grant's tokens, still sealed.</summary>
public sealed record SealedCredential(
    string AccessTokenCipher,
    string? RefreshTokenCipher,
    string KeyId,
    DateTimeOffset? ExpiresAt);
