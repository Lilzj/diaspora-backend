using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Analytics;

/// <summary>
/// How one listing has performed.
/// <para>
/// The seller's own view of their own listing, and staff's. Not a buyer-facing surface: a buyer
/// knowing that a listing has had two views and no offers would be reading a negotiating position.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.ViewAnalytics)]
public sealed record GetListingAnalyticsQuery : IQuery<ListingAnalyticsResponse>
{
    public required Guid ListingId { get; init; }

    /// <summary>How many days of the daily series to return. Thirty when omitted.</summary>
    public int? Days { get; init; }
}
