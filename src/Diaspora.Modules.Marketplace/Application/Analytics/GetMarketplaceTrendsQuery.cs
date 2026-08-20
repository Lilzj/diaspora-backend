using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Analytics;

/// <summary>
/// What is moving, by category or by country.
/// <para>
/// The brief's category and geographic trends. Staff-only, because it is a view of the whole
/// marketplace rather than of anything one member owns — and because a competitor who could read it
/// would know exactly which categories to flood.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.Moderate)]
public sealed record GetMarketplaceTrendsQuery : IQuery<IReadOnlyList<MarketplaceTrendResponse>>
{
    /// <summary><c>Category</c> or <c>Country</c>. Category when omitted.</summary>
    public string? GroupBy { get; init; }

    /// <summary>Narrow to one country, when grouping by category.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Narrow to one category, when grouping by country.</summary>
    public string? CategoryKey { get; init; }

    /// <summary>How many days back. Thirty when omitted.</summary>
    public int? Days { get; init; }

    public int? Limit { get; init; }
}
