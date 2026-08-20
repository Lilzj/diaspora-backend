namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// What the deployment's active geospatial provider can actually do.
/// <para>
/// Served so a client can hide a "search this area" control on a deployment whose provider
/// cannot answer polygon queries, rather than offering it and failing. It is also how an
/// operator confirms that the PostGIS provider they configured is the one actually running —
/// a question that otherwise has no answer short of reading logs.
/// </para>
/// </summary>
public sealed record SpatialCapabilitiesResponse
{
    /// <summary>The active provider's key.</summary>
    public required string Provider { get; init; }

    /// <summary>Every registered provider key, for diagnostics.</summary>
    public required IReadOnlyList<string> AvailableProviders { get; init; }

    /// <summary>The distance calculator in use — the earth model behind every figure.</summary>
    public required string DistanceCalculator { get; init; }

    public required bool SupportsRadiusSearch { get; init; }

    public required bool SupportsBoundingBoxSearch { get; init; }

    public required bool SupportsPolygonSearch { get; init; }

    public required bool SupportsNearestNeighbor { get; init; }

    /// <summary>Resource types with a registered nearby provider.</summary>
    public required IReadOnlyList<string> NearbyResourceTypes { get; init; }

    /// <summary>Resource types with a registered counter.</summary>
    public required IReadOnlyList<string> CountedResourceTypes { get; init; }
}
