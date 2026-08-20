using Diaspora.Modules.Location.Domain.Spatial;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// Selects the active geospatial query provider, from the several that may be registered.
/// <para>
/// The mechanism the brief's spatial-strategy requirement reduces to in practice: several
/// providers ship or are added, configuration names one, and every query in the module goes
/// through whichever was named. Swapping the relational default for PostGIS is a
/// configuration key, and the domain never learns that anything changed.
/// </para>
/// <para>
/// A configured key with no matching provider fails at startup rather than falling back
/// silently. A deployment that believes it is running PostGIS and is quietly running the
/// in-process refinement would look identical until the row count grew.
/// </para>
/// </summary>
public sealed class GeospatialProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IGeospatialQueryProvider> _providers;

    public GeospatialProviderRegistry(
        IEnumerable<IGeospatialQueryProvider> providers, IOptions<LocationSpatialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);

        var byKey = new Dictionary<string, IGeospatialQueryProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            var key = provider.Key?.Trim();

            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException(
                    $"{provider.GetType().FullName} declares no provider key.");
            }

            if (byKey.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two geospatial query providers claim the key '{key}': " +
                    $"{existing.GetType().FullName} and {provider.GetType().FullName}.");
            }

            byKey[key] = provider;
        }

        _providers = byKey;

        var configured = options.Value.QueryProvider;

        if (string.IsNullOrWhiteSpace(configured))
        {
            Active = byKey.Values.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No geospatial query provider is registered.");
            return;
        }

        Active = byKey.GetValueOrDefault(configured.Trim())
            ?? throw new InvalidOperationException(
                $"Configured geospatial provider '{configured}' is not registered. " +
                $"Available: {string.Join(", ", byKey.Keys.Order(StringComparer.Ordinal))}.");
    }

    /// <summary>The provider every spatial query in the module runs through.</summary>
    public IGeospatialQueryProvider Active { get; }

    public IReadOnlyList<string> AvailableKeys =>
        _providers.Keys.Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// Whether the active provider can answer a given kind of question. Callers check before
    /// asking so an unsupported query fails with a reason rather than with an empty list.
    /// </summary>
    public bool Supports(GeospatialCapability capability) =>
        Active.Capabilities.HasFlag(capability);
}
