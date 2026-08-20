namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// One aggregated marker on a map: a catalogue node and how much of each vertical sits in it.
/// <para>
/// <b>The cell is a real place, not a grid square.</b> A geohash tile aggregates correctly and
/// answers nothing a member can act on — tapping it can only zoom. A neighbourhood carries a name,
/// a slug, a hub page and a search every other route already accepts, so tapping a cluster is a
/// navigation rather than a zoom, and the count on it is the same number the hub will show.
/// </para>
/// </summary>
public sealed record MapClusterResponse
{
    public required Guid AreaId { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    /// <summary>The rung the cell sits on: <c>Neighborhood</c>, <c>City</c>.</summary>
    public required string Level { get; init; }

    /// <summary>The node's centre — where the marker goes.</summary>
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>
    /// Counts by resource type, from the same counters the neighbourhood hub uses.
    /// <para>
    /// An absent key means the vertical could not be asked, exactly as on the hub. A zero means it
    /// was asked and has nothing. A client that renders the two the same tells a member there is
    /// no housing in a neighbourhood when the truth is that Housing did not answer.
    /// </para>
    /// </summary>
    public required IReadOnlyDictionary<string, int> Counts { get; init; }

    /// <summary>The sum across the returned types. What a single-number marker renders.</summary>
    public required int Total { get; init; }
}
