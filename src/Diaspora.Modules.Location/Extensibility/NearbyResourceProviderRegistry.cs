namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// The composed set of nearby-resource providers, validated once at construction.
/// <para>
/// A duplicate <see cref="INearbyResourceProvider.ResourceType"/> throws here, at startup,
/// rather than resolving by assembly load order at request time. Two modules both claiming
/// <c>housing</c> is a real possibility once several verticals exist, and the failure it
/// causes — one module's results silently replacing another's, differently on each deploy —
/// is close to undiagnosable from a bug report. A boot failure costs a minute.
/// </para>
/// </summary>
public sealed class NearbyResourceProviderRegistry
{
    private readonly IReadOnlyDictionary<string, INearbyResourceProvider> _providers;

    public NearbyResourceProviderRegistry(IEnumerable<INearbyResourceProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var byType = new Dictionary<string, INearbyResourceProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            var resourceType = provider.ResourceType?.Trim();

            if (string.IsNullOrEmpty(resourceType))
            {
                throw new InvalidOperationException(
                    $"{provider.GetType().FullName} declares no resource type.");
            }

            if (byType.TryGetValue(resourceType, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two nearby-resource providers claim '{resourceType}': " +
                    $"{existing.GetType().FullName} and {provider.GetType().FullName}. " +
                    "A resource type is owned by exactly one module.");
            }

            byType[resourceType] = provider;
        }

        _providers = byType;
    }

    /// <summary>Every registered resource type, ordered for a stable response shape.</summary>
    public IReadOnlyList<string> ResourceTypes =>
        _providers.Keys.OrderBy(key => key, StringComparer.Ordinal).ToList();

    public int Count => _providers.Count;

    /// <summary>The providers that apply to this request, in a deterministic order.</summary>
    public IReadOnlyList<INearbyResourceProvider> ProvidersFor(NearbyResourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _providers.Values
            .Where(provider => provider.AppliesTo(request))
            .OrderBy(provider => provider.ResourceType, StringComparer.Ordinal)
            .ToList();
    }

    public INearbyResourceProvider? Find(string resourceType) =>
        _providers.GetValueOrDefault(resourceType);
}
