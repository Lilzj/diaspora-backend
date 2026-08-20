using System.Diagnostics;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// Asks every registered vertical what it has near a point and assembles the answer.
/// <para>
/// The isolation rules here are the whole value of the aggregate endpoint. One call replaces
/// the four the frontend's housing detail screen makes today — and the reason it made four
/// with a nested fallback chain is that any of them can fail. That failure handling moves
/// here, once:
/// </para>
/// <list type="bullet">
/// <item>Providers run <b>in turn</b>, not concurrently. They share the platform's one scoped
/// <c>DbContext</c>, which refuses a second operation while the first is running — so the
/// original fan-out threw on every provider but one and reported them all as unavailable. See
/// the comment on the loop; the parallelism was never real.</item>
/// <item>Every provider gets its own timeout, and the fan-out has a total budget. A vertical
/// having a bad day loses its own group and cannot spend the rest of the page's time.</item>
/// <item>A provider that throws loses its own group. It does not fail the request — five
/// working verticals are a better response than none.</item>
/// <item>Whatever did not answer is <em>named</em> in the response. A client that cannot tell
/// "no housing nearby" from "housing did not reply" will render the first when it means the
/// second, and the member concludes there are no homes in their area.</item>
/// </list>
/// </summary>
public sealed class NearbyAggregator
{
    private readonly NearbyResourceProviderRegistry _registry;
    private readonly IOptions<LocationSpatialOptions> _options;
    private readonly ILogger<NearbyAggregator> _logger;

    public NearbyAggregator(
        NearbyResourceProviderRegistry registry,
        IOptions<LocationSpatialOptions> options,
        ILogger<NearbyAggregator> logger)
    {
        _registry = registry;
        _options = options;
        _logger = logger;
    }

    public async Task<NearbyAggregate> AggregateAsync(
        NearbyResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var providers = _registry.ProvidersFor(request);

        if (providers.Count == 0)
        {
            return NearbyAggregate.Empty;
        }

        using var activity = LocationTelemetry.Activity.StartActivity("location.nearby.aggregate");
        activity?.SetTag("location.provider_count", providers.Count);

        var timeout = TimeSpan.FromMilliseconds(
            Math.Max(100, _options.Value.NearbyProviderTimeoutMilliseconds));

        // ── Sequential, and this is a correctness fix rather than a style choice ──────────────
        //
        // Every shipped provider answers from the platform's single scoped DiasporaDbContext.
        // EF Core refuses a second operation on a context while the first is in flight, so
        // Task.WhenAll here started six queries on one connection and threw on five of them —
        // and the per-provider catch below turned each throw into "this vertical is unavailable".
        // A member on a neighbourhood page saw one group out of six, the response said the rest
        // could not be reached, and nothing above this line was wrong.
        //
        // It stayed invisible because it needs two verticals with rows in the same place before
        // two providers even run: AppliesTo declines the rest, and the suite never had data in
        // two modules at once. The fan-out was never buying parallelism either — the shared
        // context serialises the work regardless. This awaits each in turn, which is what was
        // actually happening, minus the exceptions.
        //
        // A provider that talks to something other than the database is still isolated by its
        // own timeout, and TotalBudget stops a slow one from spending everybody else's.
        var results = new List<ProviderOutcome>(providers.Count);

        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        total.CancelAfter(TotalBudget(timeout, providers.Count));

        foreach (var provider in providers)
        {
            results.Add(await RunAsync(provider, request, timeout, total.Token, cancellationToken));
        }

        var groups = new Dictionary<string, IReadOnlyList<NearbyResourceItem>>(StringComparer.Ordinal);
        var unavailable = new List<string>();

        foreach (var result in results)
        {
            if (result.Group is null)
            {
                unavailable.Add(result.ResourceType);
                continue;
            }

            // Re-sorted defensively. The contract asks providers to order by distance and most
            // will; one that does not would otherwise scramble a list the client renders as
            // "nearest first" without checking.
            groups[result.Group.ResourceType] = result.Group.Items
                .OrderBy(item => item.DistanceKm)
                .ToList();
        }

        LocationTelemetry.NearbyProvidersQueried.Add(providers.Count);

        if (unavailable.Count > 0)
        {
            LocationTelemetry.NearbyProviderFailures.Add(unavailable.Count);
        }

        return new NearbyAggregate
        {
            Groups = groups,
            UnavailableResourceTypes = unavailable
        };
    }

    /// <summary>
    /// The whole fan-out's budget, now that it runs in turn.
    /// <para>
    /// Not <c>timeout × count</c>: six providers each allowed their full budget would let one
    /// slow deployment hold a browse screen for fifteen seconds. Twice the per-provider budget
    /// covers one straggler and stops the tail; whoever has not been asked when it expires is
    /// reported unavailable, which is the honest answer and the one the contract already has a
    /// field for.
    /// </para>
    /// </summary>
    private static TimeSpan TotalBudget(TimeSpan perProvider, int providerCount) =>
        providerCount <= 1 ? perProvider : perProvider * 2;

    private async Task<ProviderOutcome> RunAsync(
        INearbyResourceProvider provider,
        NearbyResourceRequest request,
        TimeSpan timeout,
        CancellationToken totalBudget,
        CancellationToken cancellationToken)
    {
        if (totalBudget.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Nearby provider '{ResourceType}' was not asked: the aggregate budget expired.",
                provider.ResourceType);

            return new ProviderOutcome(provider.ResourceType, null);
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(totalBudget);
        budget.CancelAfter(timeout);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var group = await provider.FindNearbyAsync(request, budget.Token);

            LocationTelemetry.NearbyProviderDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("resource_type", provider.ResourceType));

            return new ProviderOutcome(provider.ResourceType, group);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Nearby provider '{ResourceType}' exceeded its {Timeout}ms budget.",
                provider.ResourceType, timeout.TotalMilliseconds);

            return new ProviderOutcome(provider.ResourceType, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Swallowed on purpose, and reported in the response. A vertical's bug must not
            // become Location's outage.
            _logger.LogError(
                exception,
                "Nearby provider '{ResourceType}' failed.", provider.ResourceType);

            return new ProviderOutcome(provider.ResourceType, null);
        }
    }

    private sealed record ProviderOutcome(string ResourceType, NearbyResourceGroup? Group);
}
