using System.ComponentModel.DataAnnotations;

namespace Diaspora.Modules.Integrations.Options;

/// <summary>
/// What a deployment configures for the integration boundary.
/// <para>
/// <b>No credential appears on this type.</b> Not a client secret, not an API key, not a webhook
/// secret. Every one of those is read through B17's <c>ISecretStore</c> by name, which is what makes
/// rotation a store operation rather than a redeploy and what keeps them out of a bound options
/// object that a diagnostics endpoint could one day serialise. §7 and §38.
/// </para>
/// <para>
/// What is here is the shape of the boundary: which hosts may be called, how long a call may take,
/// how long a record is kept, and how far ahead an expiring grant is announced.
/// </para>
/// </summary>
public sealed class IntegrationsOptions
{
    public const string SectionName = "Integrations";

    /// <summary>
    /// The redirect URIs an authorization may return to.
    /// <para>
    /// <b>An allow-list, and the exchange refuses anything not on it.</b> "Accepting a
    /// client-supplied redirect is the canonical OAuth code-interception vulnerability." Empty means
    /// no authorization can be started, which is the correct default for a deployment that has not
    /// thought about it — the alternative default, "allow anything", is the vulnerability.
    /// </para>
    /// <para>
    /// Compared exactly after normalisation, not by prefix. A prefix match on
    /// <c>https://diaspora.app/</c> admits <c>https://diaspora.app.attacker.example/</c>.
    /// </para>
    /// </summary>
    public IList<string> AllowedRedirectUris { get; } = [];

    /// <summary>
    /// The hosts the outbound gateway may call.
    /// <para>
    /// SSRF protection, and it is host-based rather than scheme-based because
    /// <c>https://169.254.169.254/latest/meta-data/</c> is a perfectly valid HTTPS URL. Empty means
    /// no outbound call is permitted, which is what a deployment with no adapters should have.
    /// </para>
    /// </summary>
    public IList<string> AllowedOutboundHosts { get; } = [];

    /// <summary>
    /// The per-attempt timeout for a provider call.
    /// <para>
    /// Per attempt, so a hung call cannot consume the whole retry budget. §39: every outbound call
    /// has an explicit timeout and there is no way to ask for one that does not.
    /// </para>
    /// </summary>
    [Range(1, 120)]
    public int ProviderTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// The largest provider response the gateway will read, in bytes.
    /// <para>
    /// A provider that returns more than this is a provider defect, and reading it unbounded is a
    /// memory exhaustion a third party controls.
    /// </para>
    /// </summary>
    [Range(1024, 16 * 1024 * 1024)]
    public int MaxResponseBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// The largest callback body the webhook route will read, in bytes.
    /// <para>
    /// Separately configurable because that route is anonymous by design — the signature is the
    /// credential — so it is the one an unauthenticated caller can reach.
    /// </para>
    /// </summary>
    [Range(1024, 8 * 1024 * 1024)]
    public int MaxWebhookBodyBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// How long before expiry a grant is announced.
    /// <para>
    /// Seventy-two hours: long enough that somebody who only opens the app at weekends still sees
    /// it, short enough that the warning is about something imminent.
    /// </para>
    /// </summary>
    [Range(1, 720)]
    public int GrantExpiryWarningHours { get; set; } = 72;

    /// <summary>
    /// The skew applied before an access token is considered due for refresh.
    /// <para>
    /// A token expiring in four seconds will expire during the call it was checked for, and the
    /// resulting 401 is indistinguishable from a revoked grant — which would ask a member to
    /// re-authorize something that was fine.
    /// </para>
    /// </summary>
    [Range(5, 900)]
    public int TokenRefreshSkewSeconds { get; set; } = 120;

    /// <summary>
    /// How long a received callback record is kept.
    /// <para>
    /// §42 has no exception for webhook logs. Ninety days covers every provider's redelivery window
    /// with room to investigate, and is not indefinite.
    /// </para>
    /// </summary>
    [Range(1, 3650)]
    public int WebhookRetentionDays { get; set; } = 90;

    /// <summary>How long a terminal synchronisation run is kept.</summary>
    [Range(1, 3650)]
    public int SyncRunRetentionDays { get; set; } = 30;

    /// <summary>
    /// Whether the maintenance sweep runs at all.
    /// <para>
    /// <b>DEBT-049's lesson, applied on this module's first day rather than four phases in.</b> A
    /// background loop with no enable switch cannot be turned off by the deployment that needs it
    /// off — and cannot be turned off by the integration harness that shares one SQLite connection
    /// with it, which is how that debt item's non-determinism happened: a sweep writing on a
    /// background thread while a test asserts on the same connection.
    /// </para>
    /// </summary>
    public bool EnableMaintenanceSweep { get; set; } = true;

    /// <summary>
    /// How often the maintenance sweep runs.
    /// <para>
    /// One pass reaps abandoned sync runs, deletes expired authorization requests, applies the two
    /// retention horizons and announces expiring grants. One worker rather than four, because four
    /// timers over the same three tables is four leases to take and three of them for nothing.
    /// </para>
    /// </summary>
    [Range(1, 1440)]
    public int MaintenanceIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// How many rows one sweep pass touches per table.
    /// <para>
    /// Bounded so a backlog is drained over several passes rather than in one transaction that locks
    /// a table for a minute.
    /// </para>
    /// </summary>
    [Range(1, 10000)]
    public int MaintenanceBatchSize { get; set; } = 200;

    /// <summary>
    /// The maximum calendar entries one synchronisation will write.
    /// <para>
    /// A cap the client can be told about, rather than an unbounded loop against a provider's quota.
    /// A run that hits it completes as <c>partial</c> with the reason stated — never silently
    /// truncated, which would read as "everything was exported".
    /// </para>
    /// </summary>
    [Range(1, 5000)]
    public int MaxItemsPerSync { get; set; } = 200;

    /// <summary>
    /// The name the credential protector's key is stored under in B17's secret store.
    /// <para>
    /// A name, not a value. The store resolves it, the store owns its rotation, and this module
    /// never sees a key that is not the current one.
    /// </para>
    /// </summary>
    public string CredentialProtectionPurpose { get; set; } = "Diaspora.Integrations.Credentials.v1";
}
