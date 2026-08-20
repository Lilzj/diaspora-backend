using Diaspora.Platform.Operations.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Diaspora.Api.Health;

/// <summary>
/// Presents the platform health model to ASP.NET Core's health-check pipeline.
/// <para>
/// <b>One adapter for every module rather than one check per module</b>, because the aggregator already
/// runs contributors concurrently, under per-policy timeouts, with per-policy caching. Registering
/// fifteen separate checks would run them through ASP.NET's own sequencing instead and lose all three.
/// </para>
/// <para>
/// <b>Critical is the aggregator's word, not this adapter's.</b> A module whose dependency is impaired
/// reports degraded here and the instance keeps traffic; only a check whose <c>HealthPolicy</c> says
/// <c>Critical</c> produces unhealthy. That mapping is the health policy's entire purpose and putting
/// it anywhere else would let a third-party outage empty the load balancer.
/// </para>
/// </summary>
public sealed class ModuleHealthCheckAdapter : IHealthCheck
{
    private readonly IPlatformHealthAggregator _aggregator;

    public ModuleHealthCheckAdapter(IPlatformHealthAggregator aggregator)
    {
        ArgumentNullException.ThrowIfNull(aggregator);
        _aggregator = aggregator;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var model = await _aggregator.CheckAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["instance"] = model.Instance,
            ["checks"] = model.Results.Count,
            ["byDimension"] = model.ByDimension.ToDictionary(
                entry => entry.Key.ToString(), entry => entry.Value.ToString()),

            // Only the ones that are not healthy. A readiness payload listing every green check on a
            // fifteen-module platform is a payload nobody reads, and the entries that matter are lost
            // in it.
            ["impaired"] = model.Results
                .Where(result => result.State != PlatformHealthState.Healthy)
                .Select(result => new
                {
                    module = result.Module,
                    check = result.Name,
                    dimension = result.Dimension.ToString(),
                    state = result.State.ToString(),
                    description = result.Description
                })
                .ToArray()
        };

        var description = model.State == PlatformHealthState.Healthy
            ? $"{model.Results.Count} module check(s) healthy."
            : $"{model.Results.Count(result => result.State != PlatformHealthState.Healthy)} of " +
              $"{model.Results.Count} module check(s) impaired.";

        // Constructed rather than built through the Degraded/Unhealthy helpers, whose second parameter
        // is an exception rather than the data bag — a signature difference from Healthy that is easy
        // to miss and that silently drops every number in the payload.
        var status = model.State switch
        {
            PlatformHealthState.Healthy => HealthStatus.Healthy,
            PlatformHealthState.Degraded => HealthStatus.Degraded,
            _ => HealthStatus.Unhealthy
        };

        return new HealthCheckResult(status, description, exception: null, data);
    }
}
