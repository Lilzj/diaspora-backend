namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// The composed set of resource counters, validated once at construction.
/// <para>
/// Same uniqueness rule and same reason as
/// <see cref="NearbyResourceProviderRegistry"/>: a resource type belongs to one module, and
/// a collision should stop the boot rather than produce a hub screen whose numbers depend on
/// which assembly loaded first.
/// </para>
/// </summary>
public sealed class LocationResourceCounterRegistry
{
    private readonly IReadOnlyDictionary<string, ILocationResourceCounter> _counters;

    public LocationResourceCounterRegistry(IEnumerable<ILocationResourceCounter> counters)
    {
        ArgumentNullException.ThrowIfNull(counters);

        var byType = new Dictionary<string, ILocationResourceCounter>(StringComparer.OrdinalIgnoreCase);

        foreach (var counter in counters)
        {
            var resourceType = counter.ResourceType?.Trim();

            if (string.IsNullOrEmpty(resourceType))
            {
                throw new InvalidOperationException(
                    $"{counter.GetType().FullName} declares no resource type.");
            }

            if (byType.TryGetValue(resourceType, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two location resource counters claim '{resourceType}': " +
                    $"{existing.GetType().FullName} and {counter.GetType().FullName}.");
            }

            byType[resourceType] = counter;
        }

        _counters = byType;
    }

    public IReadOnlyList<ILocationResourceCounter> All =>
        _counters.Values
            .OrderBy(counter => counter.ResourceType, StringComparer.Ordinal)
            .ToList();

    public int Count => _counters.Count;
}
