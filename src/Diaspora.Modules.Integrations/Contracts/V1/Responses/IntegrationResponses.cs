namespace Diaspora.Modules.Integrations.Contracts.V1.Responses;

/// <summary>
/// One integration's live state — what the connection centre renders.
/// <para>
/// <b>Field-for-field the frontend's <c>IntegrationConnection</c></b> (<c>types/integrations.ts</c>),
/// including the two-axis <c>status</c>/<c>mode</c> split the client's own comment explains at
/// length. Reproducing the shape rather than designing one is what makes closing this gap a change
/// of prefix on the client and nothing else.
/// </para>
/// <para>
/// <b>There is no token field, and there is nowhere one could be added without a reviewer seeing
/// it.</b> The client made the same choice for the same reason — "a domain model with nowhere to put
/// one" — and this is that enforcement on the side that actually holds them.
/// </para>
/// </summary>
public sealed record IntegrationConnectionResponse
{
    /// <summary>The dotted key — <c>calendar.google</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>The domain half — <c>calendar</c>.</summary>
    public required string Domain { get; init; }

    /// <summary>
    /// One of the client's thirteen <c>IntegrationStatus</c> values, upper-snake.
    /// <para>
    /// Composed rather than stored: the persisted status is a four-member enum about a grant, and
    /// <c>SYNCING</c> / <c>SYNCED</c> come from the live sync run. See
    /// <c>Application.Mapping.IntegrationMapper</c>.
    /// </para>
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Who answered. Always <c>BACKEND_CONNECTED</c> or <c>NOT_CONFIGURED</c> from this platform.
    /// <para>
    /// Never <c>DEVELOPMENT_PROVIDER</c>: that value describes the client's own simulated arm and a
    /// server claiming it would be a server claiming to be a mock. Never <c>BACKEND_GAP</c> either —
    /// this endpoint existing is what stops that being true.
    /// </para>
    /// </summary>
    public required string Mode { get; init; }

    /// <summary>Present once connected; null in every other state.</summary>
    public IntegrationAccountResponse? Account { get; init; }

    /// <summary>
    /// What is usable right now — not what the provider theoretically supports.
    /// <para>
    /// Intersected server-side from the adapter's declared capabilities and the scopes actually
    /// granted, so a member who declined the write scope does not see a write capability.
    /// </para>
    /// </summary>
    public required IReadOnlyList<string> GrantedCapabilities { get; init; }

    /// <summary>Provider scope names actually granted. Names only.</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Null when it has never run — distinct from "ran and found nothing".</summary>
    public DateTimeOffset? LastSyncedAt { get; init; }

    public IntegrationErrorSummaryResponse? LastError { get; init; }

    /// <summary>
    /// Whether the provider-side grant was actually revoked, as opposed to only forgotten locally.
    /// <para>
    /// Served on every row rather than only on a disconnect response, so a member can see that a
    /// disconnection during a provider outage did not reach the provider.
    /// </para>
    /// </summary>
    public required bool RevokedAtProvider { get; init; }

    /// <summary>When this snapshot was taken.</summary>
    public required DateTimeOffset ObservedAt { get; init; }
}

/// <summary>The account a grant belongs to. No token, by construction.</summary>
public sealed record IntegrationAccountResponse
{
    /// <summary>Opaque, provider-scoped. Never treated as authorization.</summary>
    public required string ExternalId { get; init; }

    /// <summary>What the member recognises.</summary>
    public required string AccountLabel { get; init; }

    /// <summary>
    /// Whether the address is one of Apple's private relays.
    /// <para>
    /// Flagged so the centre can say so rather than presenting
    /// <c>@privaterelay.appleid.com</c> as the member's own email address.
    /// </para>
    /// </summary>
    public bool IsPrivateRelay { get; init; }

    public string? AvatarUrl { get; init; }
}

/// <summary>The renderable part of a failure. No stack, no provider payload, no token.</summary>
public sealed record IntegrationErrorSummaryResponse
{
    /// <summary>One of the client's thirteen <c>IntegrationErrorKind</c> values.</summary>
    public required string Kind { get; init; }

    /// <summary>Member-facing, already in plain language.</summary>
    public required string Message { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>For a support conversation. Never a token, never a request body.</summary>
    public string? CorrelationId { get; init; }
}

/// <summary>What a disconnect answers.</summary>
public sealed record IntegrationDisconnectResponse
{
    public required string Provider { get; init; }

    /// <summary>Always <c>DISCONNECTED</c>. Present because the client's contract names it.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// Whether the provider confirmed the revocation.
    /// <para>
    /// False is a real answer, not a failure: the local grant is gone either way, and a member is
    /// entitled to know that Google still lists Diaspora because Google could not be reached.
    /// </para>
    /// </summary>
    public required bool RevokedAtProvider { get; init; }
}

/// <summary>A calendar the connected account can see.</summary>
public sealed record CalendarRefResponse
{
    public required string Id { get; init; }

    /// <summary>
    /// Personal. Never logged, never in an analytics event, never cached beyond the request.
    /// </summary>
    public required string Title { get; init; }

    public string? Color { get; init; }

    /// <summary>The provider's real access role, never assumed true.</summary>
    public required bool AllowsModifications { get; init; }

    public required bool IsPrimary { get; init; }

    /// <summary>Which provider this calendar lives in.</summary>
    public required string Provider { get; init; }

    /// <summary>IANA. The calendar's own zone, which need not be the device's.</summary>
    public string? TimeZone { get; init; }
}

/// <summary>What a provider gives back once an entry exists on its side.</summary>
public sealed record CalendarEventRefResponse
{
    public required string ExternalEventId { get; init; }

    public required string CalendarId { get; init; }

    public required string Provider { get; init; }

    public required DateTimeOffset WrittenAt { get; init; }

    /// <summary>A deep link into the provider's own app, where it offers one.</summary>
    public string? ViewUrl { get; init; }

    /// <summary>
    /// True when this call changed nothing because the entry already existed unchanged.
    /// <para>
    /// Not on the client's <c>CalendarEventRef</c> and additive, so it cannot break a parse. It is
    /// what lets a client distinguish "written" from "already there" without a second read, and it
    /// is how a retried write reports honestly rather than claiming a second success.
    /// </para>
    /// </summary>
    public bool WasDeduplicated { get; init; }
}

/// <summary>A synchronisation's state. The client's <c>IntegrationSyncState</c>, verbatim.</summary>
public sealed record IntegrationSyncStateResponse
{
    /// <summary><c>never</c>, <c>syncing</c>, <c>synced</c>, <c>partial</c> or <c>failed</c>.</summary>
    public required string Phase { get; init; }

    /// <summary>The last time the backend <em>confirmed</em> a sync. Never a local guess.</summary>
    public DateTimeOffset? LastSyncedAt { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public required int ItemsSynced { get; init; }

    public required int ItemsFailed { get; init; }

    public required IReadOnlyList<string> FailureReasons { get; init; }

    /// <summary>True when something needs the member's attention rather than a retry.</summary>
    public required bool NeedsAttention { get; init; }

    /// <summary>The run's identifier, so a client can poll one run rather than a key.</summary>
    public Guid? SyncId { get; init; }
}

/// <summary>The member's calendar preferences. The client's <c>CalendarSyncSettings</c>, verbatim.</summary>
public sealed record CalendarSyncSettingsResponse
{
    public required bool Enabled { get; init; }

    public string? DefaultCalendarId { get; init; }

    public string? DefaultProvider { get; init; }

    public required bool AutoExportRsvps { get; init; }

    public required bool TwoWaySyncEnabled { get; init; }

    public required string SyncFrequency { get; init; }

    public required IReadOnlyList<int> DefaultReminderMinutes { get; init; }

    public required string TimeZoneBehaviour { get; init; }

    public required string ConflictBehaviour { get; init; }
}

/// <summary>
/// One way a member can sign in.
/// <para>
/// The client's <c>IdentityLink</c>. Composed from Identity's own credentials — a phone number and a
/// password, which every account on this platform has — plus this module's <c>identity.*</c>
/// connections. B22 owns neither half of the first, which is why <c>canUnlink</c> is computed from
/// the whole set here rather than stored on a row.
/// </para>
/// </summary>
public sealed record IdentityLinkResponse
{
    public required string Provider { get; init; }

    public required string Status { get; init; }

    public required string Mode { get; init; }

    public IntegrationAccountResponse? Account { get; init; }

    public DateTimeOffset? LinkedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>Whether this is the method the account was created with.</summary>
    public required bool IsPrimary { get; init; }

    /// <summary>
    /// False when removing it would lock the member out.
    /// <para>
    /// Computed from the whole set, never from one entry — "a rule about 'the last one' cannot be
    /// evaluated one at a time" — and enforced again on the unlink path, because "the client's
    /// disabled button is a courtesy, and a courtesy is not a control".
    /// </para>
    /// </summary>
    public required bool CanUnlink { get; init; }

    /// <summary>Why not, when <see cref="CanUnlink"/> is false.</summary>
    public string? CannotUnlinkReason { get; init; }
}

/// <summary>
/// What this deployment actually has.
/// <para>
/// The same shape B21 established for the same reason: a client should be able to discover that a
/// capability is unavailable before it offers a button that cannot work. Nothing here is a promise
/// about a provider's health — that is <see cref="IntegrationProviderStatusResponse"/> — only about
/// whether an adapter and its credentials exist.
/// </para>
/// </summary>
public sealed record IntegrationCapabilitiesResponse
{
    /// <summary>Provider keys with a configured OAuth adapter.</summary>
    public required IReadOnlyList<string> OAuthProviders { get; init; }

    /// <summary>Provider keys with a configured calendar adapter.</summary>
    public required IReadOnlyList<string> CalendarProviders { get; init; }

    /// <summary>Per calendar provider, the operations its adapter declares.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> CalendarCapabilities { get; init; }

    /// <summary>Providers whose callbacks this deployment can verify.</summary>
    public required IReadOnlyList<string> WebhookProviders { get; init; }

    /// <summary>
    /// Whether a grant can be held at all.
    /// <para>
    /// False means the credential protector has no key, which means the OAuth exchange will refuse
    /// rather than store a token in the clear. Served so an operator sees it from the capability
    /// report instead of from a member's failed authorization.
    /// </para>
    /// </summary>
    public required bool CredentialCustodyConfigured { get; init; }

    /// <summary>
    /// Whether any redirect URI is allow-listed.
    /// <para>
    /// False means no authorization can be started on this deployment, which is the correct default
    /// for one that has not configured the list and is worth being able to see.
    /// </para>
    /// </summary>
    public required bool RedirectUrisConfigured { get; init; }

    /// <summary>
    /// Two-way calendar sync.
    /// <para>
    /// Always false today, with the reason served beside it. The register calls the watch channel
    /// "the largest piece of infrastructure in this register", it needs a verified public domain,
    /// and the client's own rule is that until it exists nothing may render "synced".
    /// </para>
    /// </summary>
    public required bool TwoWayCalendarSync { get; init; }

    /// <summary>Why a capability reported false is false. Keyed by capability.</summary>
    public required IReadOnlyDictionary<string, string> UnavailableReasons { get; init; }
}

/// <summary>
/// A provider's operational state, for the operator rather than the member.
/// <para>
/// <b>Deliberately separate from the application's own health.</b> §36: a provider outage must not
/// fail <c>/health/live</c>. This is served under the module's own route so a dashboard can show
/// provider readiness without a third party's availability deciding whether this deployment is
/// considered up.
/// </para>
/// </summary>
public sealed record IntegrationProviderStatusResponse
{
    public required string Provider { get; init; }

    public required bool IsConfigured { get; init; }

    /// <summary>The B17 dependency name whose breaker and policies govern this provider.</summary>
    public required string Dependency { get; init; }
}
