namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// The counts a results page renders beside its filters — "Remote (88)", "Sponsors visas (41)".
/// <para>
/// Keyed by the same strings the search request filters on, so a client can round-trip a facet
/// into a filter without a translation table.
/// </para>
/// </summary>
public sealed record JobSearchFacetsResponse
{
    public required IReadOnlyDictionary<string, int> WorkplaceTypes { get; init; }

    public required IReadOnlyDictionary<string, int> JobTypes { get; init; }

    public required IReadOnlyDictionary<string, int> Countries { get; init; }

    public required IReadOnlyDictionary<string, int> Categories { get; init; }

    /// <summary>How many of the matching postings say they sponsor a visa.</summary>
    public required int VisaSponsorshipCount { get; init; }

    /// <summary>How many come from an employer carrying a Trust mark.</summary>
    public required int VerifiedEmployerCount { get; init; }
}
