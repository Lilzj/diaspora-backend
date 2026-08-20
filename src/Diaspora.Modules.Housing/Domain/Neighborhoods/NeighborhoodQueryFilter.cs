using Diaspora.Modules.Housing.Domain.Policies;

namespace Diaspora.Modules.Housing.Domain.Neighborhoods;

/// <summary>
/// How a neighbourhood directory is narrowed.
/// <para>
/// <b>Narrowing is by B4's geography, not by anything Housing owns.</b> A country code and a
/// materialised path prefix are both B4's values, cached on the profile so this query needs no
/// join into Location - which is what lets the neighbourhood directory stay fast without Housing
/// taking ownership of a single geographic fact.
/// </para>
/// </summary>
public sealed record NeighborhoodQueryFilter
{
    public string? CountryCode { get; init; }

    /// <summary>B4's materialised path, matched as a prefix. Every neighbourhood under a city.</summary>
    public string? AreaPathPrefix { get; init; }

    public string? CityName { get; init; }

    /// <summary>Only profiles somebody has actually written. Excludes bare shells.</summary>
    public bool WithContentOnly { get; init; }

    /// <summary>Only profiles an approver has signed off.</summary>
    public bool ApprovedOnly { get; init; }

    /// <summary>Only neighbourhoods with live properties in them. What a housing search facets by.</summary>
    public bool WithListingsOnly { get; init; }

    public string? Text { get; init; }

    /// <summary>Most listings first rather than alphabetical. What a discovery page renders.</summary>
    public bool OrderByPopularity { get; init; }

    public int Limit { get; init; } = HousingLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, HousingLimits.MaxPageSize);
}
