using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Everything a country's front page shows: seven shelves, the navigation breakdown, what people are
/// searching for and what else the platform holds.
/// <para>
/// <b>The reusable surface every module was going to build for itself.</b> Jobs wanting "new to this
/// country? start here", Housing wanting neighbourhood guidance beside a listing, Community Life
/// wanting culture guides beside a festival — each would have had its own idea of what "official"
/// means and its own bug in the fallback when a country has nothing.
/// </para>
/// </summary>
public sealed record GetCountryDashboardQuery : IQuery<CountryDashboardResponse>
{
    public required string CountryCode { get; init; }

    /// <summary>A BCP-47 tag. Labels and copy come back resolved for it.</summary>
    public string? Locale { get; init; }

    /// <summary>Narrows the shelves to one guide language.</summary>
    public string? Language { get; init; }
}
