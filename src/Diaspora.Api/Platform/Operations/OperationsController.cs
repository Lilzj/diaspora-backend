using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Platform.Http;
using Diaspora.Platform.Operations.Alerting;
using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Configuration;
using Diaspora.Platform.Operations.Dashboards;
using Diaspora.Platform.Operations.FeatureManagement;
using Diaspora.Platform.Operations.Health;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Operations.Security;
using Diaspora.Platform.Operations.Workers;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Platform.Operations;

/// <summary>
/// The operator's console for the Production Platform.
/// <para>
/// <b>Every endpoint here exists so that the answer to an operational question is a request rather
/// than a shell.</b> The alternative — reading a policy out of source, guessing a cache's hit ratio,
/// changing a setting with a redeploy — is what the platform did before B17, and it is why the same
/// three questions were asked in every incident.
/// </para>
/// <para>
/// <b>Nothing here returns a member's data, and nothing returns a secret's value.</b> Health results
/// carry operational counts by contract; the secret endpoints return descriptors and fingerprints. An
/// operational surface is reachable from more places inside a cluster than it should be, and it is
/// consulted under pressure by people who paste its output into incident channels.
/// </para>
/// <para>
/// <b>These endpoints are additive.</b> No existing route, DTO or permission key changes, so the React
/// Native client is untouched by this phase — which is Part 14's requirement and is verified in the
/// frontend contract report.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/platform/operations")]
[Produces("application/json")]
[Authorize(Policy = OperationsPolicies.Read)]
public sealed class OperationsController : ApiController
{
    private readonly IPlatformHealthAggregator _health;
    private readonly IOperationalDashboardService _dashboards;
    private readonly IOperationalPolicyCatalog _policies;
    private readonly IAlertRuleCatalog _alerts;
    private readonly IWorkerRegistry _workers;
    private readonly ICircuitBreakerRegistry _breakers;
    private readonly IBulkheadRegistry _bulkheads;
    private readonly ICacheStatisticsProvider _cacheStatistics;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly IFeatureFlagService _flags;
    private readonly IFeatureFlagAdministration _flagAdministration;
    private readonly IRuntimeConfiguration _configuration;
    private readonly IRuntimeConfigurationAdministration _configurationAdministration;
    private readonly ISecretStore _secrets;
    private readonly IBackgroundJobScheduler _jobs;
    private readonly IPrivacyAuditor _privacy;
    private readonly ICurrentUser _currentUser;

    public OperationsController(
        IPlatformHealthAggregator health,
        IOperationalDashboardService dashboards,
        IOperationalPolicyCatalog policies,
        IAlertRuleCatalog alerts,
        IWorkerRegistry workers,
        ICircuitBreakerRegistry breakers,
        IBulkheadRegistry bulkheads,
        ICacheStatisticsProvider cacheStatistics,
        ICacheInvalidator cacheInvalidator,
        IFeatureFlagService flags,
        IFeatureFlagAdministration flagAdministration,
        IRuntimeConfiguration configuration,
        IRuntimeConfigurationAdministration configurationAdministration,
        ISecretStore secrets,
        IBackgroundJobScheduler jobs,
        IPrivacyAuditor privacy,
        ICurrentUser currentUser)
    {
        _health = health;
        _dashboards = dashboards;
        _policies = policies;
        _alerts = alerts;
        _workers = workers;
        _breakers = breakers;
        _bulkheads = bulkheads;
        _cacheStatistics = cacheStatistics;
        _cacheInvalidator = cacheInvalidator;
        _flags = flags;
        _flagAdministration = flagAdministration;
        _configuration = configuration;
        _configurationAdministration = configurationAdministration;
        _secrets = secrets;
        _jobs = jobs;
        _privacy = privacy;
        _currentUser = currentUser;
    }

    // ─── Health ──────────────────────────────────────────────────────────────────────────

    /// <summary>The full platform health model for this instance.</summary>
    [HttpGet("health")]
    [ProducesResponseType<ApiResponse<PlatformHealthModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealthAsync(
        [FromQuery] HealthDimension? dimension, CancellationToken cancellationToken)
    {
        var model = await _health.CheckAsync(dimension, cancellationToken);

        return Ok(new ApiResponse<PlatformHealthModel>(model, ApiMeta.For(HttpContext)));
    }

    // ─── Dashboards ──────────────────────────────────────────────────────────────────────

    /// <summary>Which boards have contributors on this deployment.</summary>
    [HttpGet("dashboards")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<string>>>(StatusCodes.Status200OK)]
    public IActionResult GetDashboards() =>
        Ok(new ApiResponse<IReadOnlyList<string>>(_dashboards.Available, ApiMeta.For(HttpContext)));

    /// <summary>One board.</summary>
    [HttpGet("dashboards/{name}")]
    [ProducesResponseType<ApiResponse<OperationalDashboard>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboardAsync(string name, CancellationToken cancellationToken)
    {
        var dashboard = await _dashboards.BuildAsync(name, cancellationToken);

        return dashboard is null
            ? NotFound(PlatformProblem.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                PlatformErrorCodes.NotFound,
                "Unknown dashboard",
                $"No contributor produces a dashboard named '{name}'."))
            : Ok(new ApiResponse<OperationalDashboard>(dashboard, ApiMeta.For(HttpContext)));
    }

    // ─── Policies and alerts ─────────────────────────────────────────────────────────────

    /// <summary>Every operational policy in force, with its version.</summary>
    [HttpGet("policies")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<OperationalPolicyResponse>>>(StatusCodes.Status200OK)]
    public IActionResult GetPolicies() =>
        Ok(new ApiResponse<IReadOnlyList<OperationalPolicyResponse>>(
            [.. _policies.All.Select(OperationalPolicyResponse.From)],
            ApiMeta.For(HttpContext)));

    /// <summary>Every alert rule, for export to whatever evaluates them.</summary>
    [HttpGet("alerts")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AlertRule>>>(StatusCodes.Status200OK)]
    public IActionResult GetAlerts() =>
        Ok(new ApiResponse<IReadOnlyList<AlertRule>>(_alerts.All, ApiMeta.For(HttpContext)));

    // ─── Workers, resilience and cache ───────────────────────────────────────────────────

    /// <summary>Every background worker on this instance.</summary>
    [HttpGet("workers")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<WorkerStatus>>>(StatusCodes.Status200OK)]
    public IActionResult GetWorkers() =>
        Ok(new ApiResponse<IReadOnlyList<WorkerStatus>>(_workers.All, ApiMeta.For(HttpContext)));

    /// <summary>Circuit breakers and bulkheads.</summary>
    [HttpGet("resilience")]
    [ProducesResponseType<ApiResponse<ResilienceStateResponse>>(StatusCodes.Status200OK)]
    public IActionResult GetResilience() =>
        Ok(new ApiResponse<ResilienceStateResponse>(
            new ResilienceStateResponse(_breakers.All, _bulkheads.All), ApiMeta.For(HttpContext)));

    /// <summary>
    /// Holds a circuit open until it is reset.
    /// <para>
    /// The lever for a planned dependency outage: isolating a payment provider before its maintenance
    /// window converts a wave of timeouts into immediate, correctly-typed refusals.
    /// </para>
    /// </summary>
    [HttpPost("resilience/{dependency}/isolate")]
    [Authorize(Policy = OperationsPolicies.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult IsolateCircuit(string dependency)
    {
        _breakers.Isolate(dependency);

        return NoContent();
    }

    /// <summary>Returns an isolated or open circuit to closed.</summary>
    [HttpPost("resilience/{dependency}/reset")]
    [Authorize(Policy = OperationsPolicies.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult ResetCircuit(string dependency)
    {
        _breakers.Reset(dependency);

        return NoContent();
    }

    /// <summary>Cache hit ratios, per namespace.</summary>
    [HttpGet("cache")]
    [ProducesResponseType<ApiResponse<CacheStatistics>>(StatusCodes.Status200OK)]
    public IActionResult GetCache() =>
        Ok(new ApiResponse<CacheStatistics>(_cacheStatistics.Snapshot(), ApiMeta.For(HttpContext)));

    /// <summary>Invalidates a cache tag or namespace across every replica.</summary>
    [HttpPost("cache/invalidate")]
    [Authorize(Policy = OperationsPolicies.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InvalidateCacheAsync(
        [FromBody] CacheInvalidationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            await _cacheInvalidator.InvalidateTagAsync(
                request.Tag, CacheInvalidationReason.Manual, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.CacheNamespace))
        {
            await _cacheInvalidator.InvalidateNamespaceAsync(
                request.CacheNamespace, CacheInvalidationReason.Manual, cancellationToken);
        }
        else if (request.Everything)
        {
            // Deliberately the only way to reach this, and deliberately not a separate endpoint that
            // could be hit by mistake. A platform that has just discarded its whole cache sends every
            // read to the database at once.
            await _cacheInvalidator.InvalidateAllAsync(cancellationToken);
        }
        else
        {
            return BadRequest(PlatformProblem.Create(
                HttpContext,
                StatusCodes.Status400BadRequest,
                PlatformErrorCodes.Validation,
                "Nothing to invalidate",
                "Supply a tag, a namespace, or everything: true."));
        }

        return NoContent();
    }

    // ─── Feature flags ───────────────────────────────────────────────────────────────────

    /// <summary>Every declared flag.</summary>
    [HttpGet("feature-flags")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<FeatureFlag>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFlagsAsync(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<FeatureFlag>>(
            await _flags.ListAsync(cancellationToken), ApiMeta.For(HttpContext)));

    /// <summary>Turns a flag on or off for everybody.</summary>
    [HttpPut("feature-flags/{flag}")]
    [Authorize(Policy = OperationsPolicies.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetFlagAsync(
        string flag, [FromBody] FeatureFlagStateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await _flagAdministration.SetStateAsync(
                flag, request.Enabled, Actor, request.Reason, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(PlatformProblem.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                PlatformErrorCodes.NotFound,
                "Unknown feature flag",
                $"No module declares a flag named '{flag}'."));
        }

        return NoContent();
    }

    /// <summary>Recent flag changes.</summary>
    [HttpGet("feature-flags/history")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<FeatureFlagChange>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFlagHistoryAsync(
        [FromQuery] string? flag, [FromQuery] int limit, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<FeatureFlagChange>>(
            await _flagAdministration.HistoryAsync(flag, Clamp(limit), cancellationToken),
            ApiMeta.For(HttpContext)));

    /// <summary>Whether the kill switch is engaged, and why.</summary>
    [HttpGet("kill-switch")]
    [ProducesResponseType<ApiResponse<KillSwitchState>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKillSwitchAsync(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<KillSwitchState>(
            await _flagAdministration.GetKillSwitchAsync(cancellationToken), ApiMeta.For(HttpContext)));

    /// <summary>
    /// Engages or releases the kill switch.
    /// <para>
    /// A reason is required to engage. An unexplained kill switch is an incident nobody can resolve:
    /// the next person to look sees every flag off and no way to know whether turning them back on is
    /// safe.
    /// </para>
    /// </summary>
    [HttpPost("kill-switch")]
    [Authorize(Policy = OperationsPolicies.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetKillSwitchAsync(
        [FromBody] KillSwitchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Engage)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(PlatformProblem.Create(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    PlatformErrorCodes.Validation,
                    "A reason is required",
                    "Engaging the kill switch without a stated reason leaves the next person with no " +
                    "way to know whether releasing it is safe."));
            }

            await _flagAdministration.EngageKillSwitchAsync(Actor, request.Reason, cancellationToken);
        }
        else
        {
            await _flagAdministration.ReleaseKillSwitchAsync(Actor, cancellationToken);
        }

        return NoContent();
    }

    // ─── Runtime configuration ───────────────────────────────────────────────────────────

    /// <summary>Every declared setting and its current value, with sensitive values redacted.</summary>
    [HttpGet("configuration")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ConfigurationEntryResponse>>>(StatusCodes.Status200OK)]
    public IActionResult GetConfiguration() =>
        Ok(new ApiResponse<IReadOnlyList<ConfigurationEntryResponse>>(
            [.. _configuration.All.Select(ConfigurationEntryResponse.From)],
            ApiMeta.For(HttpContext)));

    /// <summary>Sets a runtime setting.</summary>
    [HttpPut("configuration/{key}")]
    [Authorize(Policy = OperationsPolicies.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetConfigurationAsync(
        string key, [FromBody] ConfigurationValueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await _configurationAdministration.SetAsync(
                key, request.Value, Actor, request.Reason, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(PlatformProblem.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                PlatformErrorCodes.NotFound,
                "Unknown setting",
                $"No module declares a runtime setting named '{key}'."));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(PlatformProblem.Create(
                HttpContext,
                StatusCodes.Status400BadRequest,
                PlatformErrorCodes.Validation,
                "Invalid value",
                exception.Message));
        }

        return NoContent();
    }

    /// <summary>Returns a setting to its declared default.</summary>
    [HttpDelete("configuration/{key}")]
    [Authorize(Policy = OperationsPolicies.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetConfigurationAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _configurationAdministration.ResetAsync(key, Actor, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(PlatformProblem.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                PlatformErrorCodes.NotFound,
                "Unknown setting",
                $"No module declares a runtime setting named '{key}'."));
        }

        return NoContent();
    }

    /// <summary>Recent configuration changes, with sensitive values redacted.</summary>
    [HttpGet("configuration/history")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ConfigurationChange>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfigurationHistoryAsync(
        [FromQuery] string? key, [FromQuery] int limit, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<ConfigurationChange>>(
            await _configurationAdministration.HistoryAsync(key, Clamp(limit), cancellationToken),
            ApiMeta.For(HttpContext)));

    // ─── Secrets ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Secret metadata: version, status, expiry and fingerprint.
    /// <para>
    /// Never a value, and not because a caller could not be trusted with one — because a listing
    /// endpoint that returned values would put every secret on the platform into one response, one
    /// log line and one screenshot.
    /// </para>
    /// </summary>
    [HttpGet("secrets")]
    [Authorize(Policy = OperationsPolicies.Secrets)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SecretDescriptor>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSecretsAsync(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<SecretDescriptor>>(
            await _secrets.DescribeAllAsync(cancellationToken), ApiMeta.For(HttpContext)));

    /// <summary>Which declared secrets are missing, malformed or overdue.</summary>
    [HttpGet("secrets/validate")]
    [Authorize(Policy = OperationsPolicies.Secrets)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<string>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateSecretsAsync(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<string>>(
            await _secrets.ValidateAsync(cancellationToken), ApiMeta.For(HttpContext)));

    // ─── Jobs and privacy ────────────────────────────────────────────────────────────────

    /// <summary>Scheduled jobs, newest first.</summary>
    [HttpGet("jobs")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ScheduledJob>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobsAsync(
        [FromQuery] JobStatus? status, [FromQuery] int limit, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<ScheduledJob>>(
            await _jobs.ListAsync(status, Clamp(limit), cancellationToken), ApiMeta.For(HttpContext)));

    /// <summary>Cancels a pending or running job.</summary>
    [HttpDelete("jobs/{jobId}")]
    [Authorize(Policy = OperationsPolicies.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelJobAsync(string jobId, CancellationToken cancellationToken) =>
        await _jobs.CancelAsync(jobId, cancellationToken)
            ? NoContent()
            : NotFound(PlatformProblem.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                PlatformErrorCodes.NotFound,
                "Unknown or finished job",
                $"Job '{jobId}' is unknown, or has already finished."));

    /// <summary>
    /// Which modules can export, erase and age out their data — and which cannot.
    /// <para>
    /// The findings are the point. A module with no erasure provider cannot honour a deletion request,
    /// and nothing else on the platform would surface that until one arrived.
    /// </para>
    /// </summary>
    [HttpGet("privacy-audit")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PrivacyAuditEntry>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrivacyAuditAsync(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyList<PrivacyAuditEntry>>(
            await _privacy.AuditAsync(cancellationToken), ApiMeta.For(HttpContext)));

    /// <summary>
    /// Who is making the change, for the audit trail.
    /// <para>
    /// The user id rather than a display name: a name is not stable and an audit entry naming somebody
    /// who has since changed theirs is an audit entry nobody can resolve.
    /// </para>
    /// </summary>
    private string Actor => _currentUser.UserId?.ToString() ?? "unknown";

    private static int Clamp(int limit) => limit is <= 0 or > 500 ? 100 : limit;
}
