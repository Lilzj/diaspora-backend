using System.Text.Json;
using Diaspora.Persistence;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Modules;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Diaspora.Api.Health;

/// <summary>
/// Liveness and readiness that mean something.
/// <para>
/// B2's readiness check was <c>() =&gt; HealthCheckResult.Healthy("API is ready.")</c>. It
/// returned 200 with the database down, which makes it worse than no probe at all: an
/// orchestrator routes traffic to an instance that cannot serve a single request, and does
/// so confidently.
/// </para>
/// <para>
/// The split is the standard one and it matters. Liveness answers "should this process be
/// killed and replaced?" and must not depend on anything external — a database outage that
/// fails liveness triggers a restart storm that makes the outage worse. Readiness answers
/// "should this instance receive traffic right now?" and depends on everything the instance
/// needs.
/// </para>
/// </summary>
public static class PlatformHealthChecks
{
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    public static IServiceCollection AddPlatformHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy("The process is running."),
                tags: [LiveTag])

            // Actually opens a connection. AddDbContextCheck's default is CanConnectAsync,
            // which is what "can this instance serve a request" reduces to.
            .AddDbContextCheck<DiasporaDbContext>(
                "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag])

            .AddCheck<ModuleCatalogHealthCheck>(
                "modules",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag])

            // Readiness, not liveness. A backed-up outbox means this instance should stop
            // taking write traffic that will add to a queue nothing is draining; it emphatically
            // does not mean the process should be killed and replaced, which would abandon
            // whatever leases it holds and make the backlog worse.
            .AddCheck<OutboxHealthCheck>(
                "outbox",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag])

            // Reports how transactions execute on this deployment — whether the provider retries,
            // and therefore whether commit verification is in play — plus whether the verification
            // receipts are being collected. It degrades and never fails: a request that cannot
            // commit says so in its own response, and removing the instance would not help.
            .AddCheck<TransactionHealthCheck>(
                "transactions",
                failureStatus: HealthStatus.Degraded,
                tags: [ReadyTag])

            // B17. Every module's own health, through the platform aggregator: one registration rather
            // than one per module, because the aggregator runs contributors concurrently under
            // per-policy timeouts and caching, and fifteen separate ASP.NET checks would lose all three.
            //
            // Degraded is the declared failure status and the aggregator decides the rest: a module
            // whose third-party dependency is impaired must not empty the load balancer, and only a
            // check whose HealthPolicy says Critical reaches Unhealthy.
            .AddCheck<ModuleHealthCheckAdapter>(
                "modules.health",
                failureStatus: HealthStatus.Degraded,
                tags: [ReadyTag]);

        return services;
    }

    /// <summary>
    /// Writes health results as JSON, so a human hitting the endpoint during an incident
    /// learns which dependency is down rather than the single word "Unhealthy".
    /// </summary>
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description,

                // B1.1 added this. The outbox check's answer is not a word but a set of
                // numbers — depth, retry backlog, dead letters, the age of the oldest
                // undelivered message, the per-module breakdown — and a status alone tells an
                // operator that something is wrong without telling them which module. Every
                // check's data travels because a per-check allowlist would go stale the first
                // time somebody added a check and forgot.
                //
                // The rule that keeps this safe is on the producing side: a check puts
                // operational counts in its data and never a payload, an identifier or
                // anything a member wrote. The outbox check follows it deliberately, which is
                // why its listings carry event types and never event bodies.
                data = entry.Value.Data.Count == 0 ? null : entry.Value.Data,

                // The exception message, never the stack trace. A readiness endpoint is
                // typically reachable from inside the cluster by more things than it should be.
                error = entry.Value.Exception?.Message
            })
        };

        return context.Response.WriteAsJsonAsync(payload, JsonSerializerOptions.Web);
    }
}

/// <summary>
/// Reports that the platform composed itself successfully.
/// <para>
/// The catalogues are built lazily on first resolve, which means a duplicate permission key
/// or a policy naming a permission nothing declares would surface on the first request that
/// touched authorisation rather than at startup. Resolving them here turns that into a
/// readiness failure — the instance never takes traffic — and gives the deployment a
/// specific reason instead of a stream of 500s.
/// </para>
/// </summary>
public sealed class ModuleCatalogHealthCheck : IHealthCheck
{
    private readonly ModuleRegistry _modules;
    private readonly IPermissionCatalog _permissions;
    private readonly IFeatureCatalog _features;
    private readonly IPolicyCatalog _policies;

    public ModuleCatalogHealthCheck(
        ModuleRegistry modules,
        IPermissionCatalog permissions,
        IFeatureCatalog features,
        IPolicyCatalog policies)
    {
        _modules = modules;
        _permissions = permissions;
        _features = features;
        _policies = policies;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modules"] = _modules.Modules.Select(m => $"{m.Name} {m.Version}").ToArray(),
                ["permissions"] = _permissions.All.Count,
                ["features"] = _features.All.Count,
                ["policies"] = _policies.All.Count
            };

            if (_modules.Modules.Count == 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "No platform modules were discovered. The host has nothing to serve.",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"{_modules.Modules.Count} module(s) composed.", data));
        }
        catch (InvalidOperationException exception)
        {
            // Thrown by the catalogues on a duplicate declaration or a dangling policy
            // reference. Both are configuration errors that should stop a rollout.
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "The platform catalogues are inconsistent.", exception));
        }
    }
}
