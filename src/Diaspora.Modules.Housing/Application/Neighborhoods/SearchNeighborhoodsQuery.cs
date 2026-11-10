using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// The neighbourhood directory.
/// <para>
/// The reusable discovery surface the brief asks for: a client narrows by country or by a Location
/// area subtree and gets back places with housing in them, ordered by where members actually are.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record SearchNeighborhoodsQuery : IQuery<IReadOnlyList<NeighborhoodSummaryResponse>>
{
    public string? CountryCode { get; init; }

    /// <summary>Any Location area. Resolved to its subtree, so a city returns its neighbourhoods.</summary>
    public Guid? AreaId { get; init; }

    public string? City { get; init; }

    public string? Query { get; init; }

    /// <summary>Only neighbourhoods with live properties. What a housing search facets by.</summary>
    public bool WithListingsOnly { get; init; }

    /// <summary>Most listings first rather than alphabetical.</summary>
    public bool ByPopularity { get; init; } = true;

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
