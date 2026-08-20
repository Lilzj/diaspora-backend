using System.Diagnostics;
using System.Diagnostics.Metrics;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Integrations.Observability;

/// <summary>
/// The module's activity source and meters.
/// <para>
/// <b>Nothing here carries a secret, and the enforcement is the tag set rather than a redaction
/// filter.</b> §35 names what must never reach telemetry — access tokens, refresh tokens, API keys,
/// authorization codes, webhook secrets — and every tag emitted below is one of five
/// low-cardinality labels: a provider key, a capability, an outcome, an error *category*, or a
/// dependency name. There is no tag for a member, an account label, a calendar title, an external
/// identifier or a URL. A counter tagged with a calendar id would be a per-member index of somebody's
/// calendars with a metrics bill attached.
/// </para>
/// <para>
/// <b>The pairing that matters most is <see cref="NotConfigured"/> against
/// <see cref="OutboundRequests"/>.</b> A deployment whose not-configured count is most of its
/// integration traffic has members repeatedly asking for something nobody contracted, and that is
/// invisible from any infrastructure dashboard: every endpoint is healthy, fast, and refusing.
/// </para>
/// </summary>
public static class IntegrationsTelemetry
{
    /// <summary>The name the observability contributor subscribes the host's tracer to.</summary>
    public const string SourceName = "Diaspora.Integrations";

    public static ActivitySource Source { get; } = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    // ── Outbound ────────────────────────────────────────────────────────────────────────────

    /// <summary>Calls offered to a provider, tagged by dependency. §35's <c>integration_requests_total</c>.</summary>
    public static Counter<long> OutboundRequests { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.requests", "request", "Calls offered to an external provider.");

    /// <summary>Round-trip duration, tagged by dependency and status class.</summary>
    public static Histogram<double> OutboundDuration { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.integrations.request.duration", "ms", "External provider call duration.");

    /// <summary>Calls that did not succeed, tagged by dependency and a failure *category*.</summary>
    public static Counter<long> OutboundFailures { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.failures", "request",
            "External provider calls that returned other than success.");

    /// <summary>
    /// Calls refused by the allow-list before a socket was opened.
    /// <para>
    /// Worth its own counter and worth alerting on. A non-zero rate is either a misconfigured
    /// endpoint or an attempt to make this platform fetch something it should not — and the second
    /// is a finding, not a metric.
    /// </para>
    /// </summary>
    public static Counter<long> OutboundBlocked { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.outbound.blocked", "request",
            "Outbound calls refused by the destination allow-list.");

    /// <summary>Provider refusals carrying a quota signal. §35's <c>integration_rate_limited_total</c>.</summary>
    public static Counter<long> RateLimited { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.rate_limited", "request",
            "Provider calls the provider asked us to slow down.");

    /// <summary>
    /// Requests answered <c>NotConfigured</c>. §35's <c>integration_not_configured_total</c>.
    /// <para>
    /// Tagged by capability rather than by provider, because the operational question is "what are
    /// members asking for that this deployment cannot do" and a provider tag answers a different one.
    /// </para>
    /// </summary>
    public static Counter<long> NotConfigured { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.not_configured", "request",
            "Requests refused because this deployment has no provider for them.");

    // ── Authorization ───────────────────────────────────────────────────────────────────────

    /// <summary>Authorizations started, tagged by provider and intent.</summary>
    public static Counter<long> AuthorizationsStarted { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.oauth.started", "authorization",
            "Authorization flows begun.");

    /// <summary>
    /// Exchanges that failed, tagged by provider and reason. §35's
    /// <c>integration_auth_failures_total</c>.
    /// <para>
    /// The reasons are this module's own — <c>state_unknown</c>, <c>state_expired</c>,
    /// <c>code_replayed</c>, <c>verifier_mismatch</c>, <c>redirect_mismatch</c>,
    /// <c>account_conflict</c>, <c>provider</c> — and every one of the first five is a security
    /// signal rather than an availability one. A rising <c>state_unknown</c> is somebody posting
    /// callbacks this platform never issued.
    /// </para>
    /// </summary>
    public static Counter<long> AuthorizationFailures { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.oauth.failures", "authorization",
            "Authorization exchanges refused.");

    /// <summary>Access tokens refreshed, tagged by provider and outcome.</summary>
    public static Counter<long> TokenRefreshes { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.token.refreshes", "refresh",
            "Access-token refresh attempts.");

    // ── Connections ─────────────────────────────────────────────────────────────────────────

    /// <summary>Connections established, tagged by provider.</summary>
    public static Counter<long> ConnectionsEstablished { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.connections.established", "connection",
            "External accounts connected.");

    /// <summary>
    /// Connections dropped, tagged by provider and whether the provider side was revoked.
    /// <para>
    /// The second tag is the one that matters: a deployment where most disconnections did not reach
    /// the provider is a deployment holding refresh tokens for accounts members believe they have
    /// detached.
    /// </para>
    /// </summary>
    public static Counter<long> ConnectionsDisconnected { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.connections.disconnected", "connection",
            "External accounts disconnected.");

    /// <summary>Connections moved to reauthorization-required, tagged by provider and cause.</summary>
    public static Counter<long> ReauthorizationsRequired { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.connections.reauth_required", "connection",
            "Grants that stopped working and need the member.");

    // ── Webhooks ────────────────────────────────────────────────────────────────────────────

    /// <summary>Callbacks received, tagged by provider and outcome. §35's <c>integration_webhooks_total</c>.</summary>
    public static Counter<long> WebhooksReceived { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.webhooks", "callback", "Provider callbacks received.");

    /// <summary>
    /// Callbacks refused, tagged by provider and reason. §35's
    /// <c>integration_webhook_failures_total</c>.
    /// <para>
    /// <c>signature_invalid</c> is the reason worth paging on: it is either a rotated secret nobody
    /// updated — in which case real callbacks are being dropped right now — or somebody probing.
    /// </para>
    /// </summary>
    public static Counter<long> WebhookFailures { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.webhook.failures", "callback",
            "Provider callbacks refused before processing.");

    // ── Synchronisation ─────────────────────────────────────────────────────────────────────

    /// <summary>Sync runs started, tagged by provider and direction.</summary>
    public static Counter<long> SyncsStarted { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.sync.started", "run", "Synchronisations begun.");

    /// <summary>Sync duration, tagged by provider and terminal phase. §35's <c>integration_sync_duration</c>.</summary>
    public static Histogram<double> SyncDuration { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.integrations.sync.duration", "ms", "Synchronisation duration.");

    /// <summary>Sync runs that ended other than <c>synced</c>. §35's <c>integration_sync_failures_total</c>.</summary>
    public static Counter<long> SyncFailures { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.sync.failures", "run",
            "Synchronisations that ended partial or failed.");

    // ── Calendar writes ─────────────────────────────────────────────────────────────────────

    /// <summary>Calendar entries written, tagged by provider and operation.</summary>
    public static Counter<long> CalendarWrites { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.calendar.writes", "entry",
            "Calendar entries created, updated or removed.");

    /// <summary>
    /// Writes suppressed because the entry already existed unchanged, tagged by provider.
    /// <para>
    /// The duplicate-suppression counter. A zero here after a redeploy that changed the fingerprint
    /// means every exported event is about to be rewritten, which is worth seeing before a member
    /// reports it.
    /// </para>
    /// </summary>
    public static Counter<long> CalendarWritesDeduplicated { get; } =
        Meter.CreateCounter<long>(
            "diaspora.integrations.calendar.deduplicated", "entry",
            "Calendar writes suppressed as already-written.");

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Records a completed outbound call. Status is bucketed, never recorded exactly.</summary>
    public static void RecordOutbound(string dependency, int? statusCode, TimeSpan elapsed)
    {
        var statusClass = statusCode switch
        {
            null => "none",
            >= 200 and < 300 => "2xx",
            >= 300 and < 400 => "3xx",
            401 or 403 => "auth",
            429 => "429",
            >= 400 and < 500 => "4xx",
            _ => "5xx"
        };

        OutboundRequests.Add(
            1,
            new KeyValuePair<string, object?>("dependency", dependency),
            new KeyValuePair<string, object?>("status", statusClass));

        OutboundDuration.Record(
            elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("dependency", dependency),
            new KeyValuePair<string, object?>("status", statusClass));

        if (statusCode is 429)
        {
            RateLimited.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
        }

        if (statusCode is >= 400)
        {
            OutboundFailures.Add(
                1,
                new KeyValuePair<string, object?>("dependency", dependency),
                new KeyValuePair<string, object?>("reason", statusClass));
        }
    }

    /// <summary>Records a call that never reached the provider.</summary>
    public static void RecordOutboundFailure(string dependency, string reason)
    {
        OutboundRequests.Add(
            1,
            new KeyValuePair<string, object?>("dependency", dependency),
            new KeyValuePair<string, object?>("status", "none"));

        OutboundFailures.Add(
            1,
            new KeyValuePair<string, object?>("dependency", dependency),
            new KeyValuePair<string, object?>("reason", reason));
    }
}

/// <summary>
/// Subscribes the host's tracer and metrics to the module's telemetry source.
/// <para>
/// The <c>ActivitySource</c> and <c>Meter</c> behind this name are created in
/// <see cref="IntegrationsTelemetry"/>. Declaring a source without creating one subscribes the
/// tracer to silence, which is the mistake the contributor contract calls out.
/// </para>
/// </summary>
public sealed class IntegrationsObservabilityContributor : IObservabilityContributor
{
    public string Module => IntegrationsModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [IntegrationsTelemetry.SourceName];

    public IEnumerable<string> Meters => [IntegrationsTelemetry.SourceName];
}
