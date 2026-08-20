using Diaspora.Modules.Location.Extensibility;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>What every vertical answered, and which of them did not.</summary>
public sealed record NearbyAggregate
{
    public required IReadOnlyDictionary<string, IReadOnlyList<NearbyResourceItem>> Groups { get; init; }

    /// <summary>
    /// Resource types that were asked and did not answer. Carried through to the response so
    /// a client can distinguish "nothing here" from "we could not find out".
    /// </summary>
    public required IReadOnlyList<string> UnavailableResourceTypes { get; init; }

    public static NearbyAggregate Empty { get; } = new()
    {
        Groups = new Dictionary<string, IReadOnlyList<NearbyResourceItem>>(StringComparer.Ordinal),
        UnavailableResourceTypes = []
    };
}
