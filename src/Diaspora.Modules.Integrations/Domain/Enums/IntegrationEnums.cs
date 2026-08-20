namespace Diaspora.Modules.Integrations.Domain.Enums;

/// <summary>
/// Who a connection belongs to.
/// <para>
/// Modelled from the start even though only <see cref="User"/> is reachable today, because the
/// alternative — a bare <c>UserId</c> column — makes "whose credential is this" a question the
/// schema cannot express, and §11 of the brief is explicit that integration credentials must have
/// explicit ownership. Every query filters on the pair, so a community connection added later
/// cannot be returned to a member by an existing handler that forgot to check.
/// </para>
/// </summary>
public enum IntegrationOwnerKind
{
    /// <summary>A member's own account. The only kind a member can create today.</summary>
    User = 0,

    /// <summary>A community's shared account. No route creates one; the schema supports it.</summary>
    Community = 1,

    /// <summary>An organization's shared account. Same.</summary>
    Organization = 2,

    /// <summary>The deployment's own account, configured by an operator rather than a member.</summary>
    Platform = 3
}

/// <summary>
/// A connection's state, server-owned so two devices agree.
/// <para>
/// A deliberate subset of the frontend's thirteen <c>IntegrationStatus</c> values, and the subset
/// is the point: <c>CONNECTING</c>, <c>SYNCING</c>, <c>PERMISSION_DENIED</c>, <c>BACKEND_GAP</c> and
/// <c>DEVELOPMENT</c> are all states of a *client*, a *sync run* or a *build*, not of a stored
/// grant. Storing them here would mean two sources of truth for the same word.
/// <see cref="Diaspora.Modules.Integrations.Application.Mapping.IntegrationMapper"/> composes the
/// client's value from this plus the live sync run.
/// </para>
/// </summary>
public enum IntegrationConnectionStatus
{
    /// <summary>A grant that works. The provider accepted the last call made with it.</summary>
    Connected = 0,

    /// <summary>
    /// The grant is gone or no longer sufficient and the member must authorize again.
    /// <para>
    /// Reached three ways: a refresh that the provider refused, a scope the member later removed,
    /// and a provider-initiated revocation signal arriving on the webhook route. The third is the
    /// one the register cares about — it is what stops the centre "claiming a grant that no longer
    /// exists".
    /// </para>
    /// </summary>
    ReauthorizationRequired = 1,

    /// <summary>The member disconnected. Kept rather than deleted; see the identifier's summary.</summary>
    Disconnected = 2,

    /// <summary>
    /// The last call failed for a reason that is neither authorization nor absence.
    /// <para>
    /// Distinct from <see cref="ReauthorizationRequired"/> because the recovery differs: a member
    /// can retry an error and cannot retry their way out of a revoked grant.
    /// </para>
    /// </summary>
    Error = 3
}

/// <summary>Why an authorization is being performed.</summary>
public enum OAuthIntent
{
    /// <summary>Attach an external account to the signed-in member's account.</summary>
    Link = 0,

    /// <summary>Replace a grant that expired or was revoked. Requires an existing connection.</summary>
    Reauthorize = 1,

    /// <summary>
    /// Sign in with the external account.
    /// <para>
    /// Accepted by the contract and refused by this deployment: minting a platform session from an
    /// external assertion is Identity's decision and Identity has no external credential model. The
    /// refusal is an explicit <c>IntentUnsupported</c> rather than a silent downgrade to
    /// <see cref="Link"/>, which would attach an account to whichever session happened to be open.
    /// </para>
    /// </summary>
    SignIn = 2
}

/// <summary>Which way data moves in a synchronisation.</summary>
public enum SyncDirection
{
    /// <summary>Diaspora writes into the provider. The only direction that ships.</summary>
    Export = 0,

    /// <summary>The provider's entries are read into Diaspora. Requires a consenting adapter.</summary>
    Import = 1,

    /// <summary>Both.</summary>
    Both = 2
}

/// <summary>
/// Where a synchronisation is. The frontend's <c>IntegrationSyncPhase</c>, verbatim.
/// <para>
/// <see cref="Partial"/> earns its place for the reason the register gives: "a calendar export that
/// wrote eleven of twelve events is not a success and is not a failure, and rounding it to either is
/// how a member ends up believing an event is in their calendar when it is not."
/// </para>
/// </summary>
public enum SyncPhase
{
    Never = 0,
    Syncing = 1,
    Synced = 2,
    Partial = 3,
    Failed = 4
}

/// <summary>What happened to one calendar entry Diaspora wrote.</summary>
public enum CalendarLinkState
{
    /// <summary>Written and believed to be present.</summary>
    Written = 0,

    /// <summary>Deleted at Diaspora's request. Kept so a re-export is a create rather than a repair.</summary>
    Removed = 1,

    /// <summary>
    /// The provider no longer has it and Diaspora did not remove it.
    /// <para>
    /// A member deleting an exported entry in their own calendar is a legitimate act, and treating
    /// it as an error would produce a sync that keeps putting it back.
    /// </para>
    /// </summary>
    RemovedAtProvider = 2
}

/// <summary>How an inbound callback was disposed of.</summary>
public enum IntegrationWebhookOutcome
{
    /// <summary>Verified, understood and acted on.</summary>
    Applied = 0,

    /// <summary>Verified and understood, and there was nothing to change.</summary>
    Ignored = 1,

    /// <summary>Verified but naming a connection this deployment does not hold.</summary>
    Unmatched = 2,

    /// <summary>Verified and refused by a rule. Recorded so a refusal is not a silence.</summary>
    Refused = 3
}

/// <summary>
/// Why an outbound provider call did not succeed.
/// <para>
/// <b>This is the whole of §22 and §49.</b> Every adapter maps its own failures into exactly one of
/// these before the result leaves it, and there is no member of this enum that means "it worked".
/// A collapsed vocabulary — one <c>Failed</c>, or worse an exception swallowed into a success — is
/// the fake-success the brief forbids, and the shape here is what makes it unrepresentable.
/// </para>
/// </summary>
public enum IntegrationFailureKind
{
    /// <summary>No adapter, or an adapter with no credential. Nothing was called.</summary>
    NotConfigured = 0,

    /// <summary>The capability exists on this platform and not on this provider.</summary>
    Unsupported = 1,

    /// <summary>The provider refused the credential. The grant needs replacing.</summary>
    Unauthorized = 2,

    /// <summary>The provider refused the request. Retrying produces the same answer.</summary>
    InvalidRequest = 3,

    /// <summary>The provider asked to be called less often.</summary>
    RateLimited = 4,

    /// <summary>The provider could not be reached, or answered 5xx, or timed out.</summary>
    Unavailable = 5,

    /// <summary>The provider answered, and what it said was an error this platform cannot classify.</summary>
    ProviderError = 6
}
