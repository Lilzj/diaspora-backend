using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// Collects the headline counts a neighbourhood hub or country page renders.
/// <para>
/// Same isolation rules as <see cref="NearbyAggregator"/>, and the same reasoning: a counter
/// that fails or is slow omits its own key rather than failing the screen. The distinction
/// that matters is between an absent key and a zero. A vertical with nothing in the area
/// reports zero; a vertical that could not be asked reports nothing at all, and the client
/// renders a dash rather than a confident and wrong "0 homes available".
/// </para>
/// </summary>
public sealed class ResourceCountAggregator
{
    private readonly LocationResourceCounterRegistry _registry;
    private readonly IOptions<LocationSpatialOptions> _options;
    private readonly ILogger<ResourceCountAggregator> _logger;

    public ResourceCountAggregator(
        LocationResourceCounterRegistry registry,
        IOptions<LocationSpatialOptions> options,
        ILogger<ResourceCountAggregator> logger)
    {
        _registry = registry;
        _options = options;
        _logger = logger;
    }

    public Task<IReadOnlyDictionary<string, int>> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default) =>
        CountInAreaAsync(areaId, path, resourceTypes: null, cancellationToken);

    /// <summary>
    /// The counts for one area, optionally narrowed to the resource types the caller asked for.
    /// <para>
    /// The narrowing is what makes a map layer affordable: counters run in turn, so a viewport
    /// asking for one vertical across twenty cells costs twenty queries rather than a hundred
    /// and twenty. An unknown type in the list narrows to nothing rather than being ignored — a
    /// client that asked for <c>housng</c> should get an empty answer it can see, not everything.
    /// </para>
    /// </summary>
    public Task<IReadOnlyDictionary<string, int>> CountInAreaAsync(
        AreaId areaId,
        HierarchyPath path,
        IReadOnlyCollection<string>? resourceTypes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        ArgumentNullException.ThrowIfNull(path);

        return CollectAsync(
            (counter, token) => counter.CountInAreaAsync(areaId, path, token),
            resourceTypes,
            cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, int>> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        return CollectAsync(
            (counter, token) => counter.CountInCountryAsync(countryCode, token),
            resourceTypes: null,
            cancellationToken);
    }

    /// <summary>The resource types this deployment can count, for a contract that publishes them.</summary>
    public IReadOnlyList<string> ResourceTypes =>
        _registry.All.Select(counter => counter.ResourceType).ToList();

    private async Task<IReadOnlyDictionary<string, int>> CollectAsync(
        Func<ILocationResourceCounter, CancellationToken, Task<int>> count,
        IReadOnlyCollection<string>? resourceTypes,
        CancellationToken cancellationToken)
    {
        var counters = resourceTypes is { Count: > 0 }
            ? _registry.All
                .Where(counter => resourceTypes.Contains(
                    counter.ResourceType, StringComparer.OrdinalIgnoreCase))
                .ToList()
            : _registry.All;

        if (counters.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var timeout = TimeSpan.FromMilliseconds(
            Math.Max(100, _options.Value.ResourceCounterTimeoutMilliseconds));

        // Sequential, for the same reason as NearbyAggregator: every shipped counter answers
        // from the one scoped DiasporaDbContext, and EF Core refuses a second operation on a
        // context while the first is running. Task.WhenAll here threw on all but one counter and
        // the catch below turned each throw into an omitted key — which this class's own contract
        // defines as "we could not find out". A neighbourhood hub rendered dashes for five of its
        // six numbers and said nothing was wrong.
        var outcomes = new List<CounterOutcome>(counters.Count);

        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        total.CancelAfter(counters.Count <= 1 ? timeout : timeout * 2);

        foreach (var counter in counters)
        {
            outcomes.Add(await RunAsync(counter, count, timeout, total.Token, cancellationToken));
        }

        return outcomes
            .Where(outcome => outcome.Count is not null)
            .ToDictionary(
                outcome => outcome.ResourceType,
                outcome => outcome.Count!.Value,
                StringComparer.Ordinal);
    }

    private async Task<CounterOutcome> RunAsync(
        ILocationResourceCounter counter,
        Func<ILocationResourceCounter, CancellationToken, Task<int>> count,
        TimeSpan timeout,
        CancellationToken totalBudget,
        CancellationToken cancellationToken)
    {
        if (totalBudget.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Resource counter '{ResourceType}' was not asked: the aggregate budget expired.",
                counter.ResourceType);

            return new CounterOutcome(counter.ResourceType, null);
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(totalBudget);
        budget.CancelAfter(timeout);

        try
        {
            return new CounterOutcome(counter.ResourceType, await count(counter, budget.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Resource counter '{ResourceType}' exceeded its {Timeout}ms budget.",
                counter.ResourceType, timeout.TotalMilliseconds);

            return new CounterOutcome(counter.ResourceType, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception, "Resource counter '{ResourceType}' failed.", counter.ResourceType);

            return new CounterOutcome(counter.ResourceType, null);
        }
    }

    private sealed record CounterOutcome(string ResourceType, int? Count);
}
