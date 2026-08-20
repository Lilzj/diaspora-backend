using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Analytics;

/// <summary>
/// Reads a listing's performance over a window.
/// <para>
/// <b>The window is applied after materialisation, and the daily grain is what makes that
/// acceptable.</b> EF cannot filter an owned collection inside an <c>Include</c>, so the whole
/// series comes back — which for a listing that has been up for three years is about a thousand
/// rows. A finer grain would have made this the wrong shape.
/// </para>
/// </summary>
public sealed class GetListingAnalyticsQueryHandler
    : IQueryHandler<GetListingAnalyticsQuery, ListingAnalyticsResponse>
{
    private const int DefaultWindowDays = 30;

    private readonly IListingAnalyticsRepository _analytics;
    private readonly IMarketplaceListingRepository _listings;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public GetListingAnalyticsQueryHandler(
        IListingAnalyticsRepository analytics,
        IMarketplaceListingRepository listings,
        MarketplaceAuthorizationService authorization,
        IClock clock)
    {
        _analytics = analytics;
        _listings = listings;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<ListingAnalyticsResponse>> Handle(
        GetListingAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var listingId = MarketplaceListingId.From(query.ListingId);
        var listing = await _listings.GetForReadAsync(listingId, cancellationToken);

        if (listing is null)
        {
            return Result.Failure<ListingAnalyticsResponse>(MarketplaceErrors.ListingNotFound);
        }

        var owns = await _authorization.OwnsAsync(listing, cancellationToken);

        if (!owns && !_authorization.IsStaff)
        {
            return Result.Failure<ListingAnalyticsResponse>(MarketplaceErrors.NotListingOwner);
        }

        var window = Math.Clamp(query.Days ?? DefaultWindowDays, 1, 365);
        var from = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime).AddDays(-window);

        var analytics = await _analytics.GetForReadAsync(listingId, from, cancellationToken);

        if (analytics is null)
        {
            return Result.Failure<ListingAnalyticsResponse>(MarketplaceErrors.ListingNotFound);
        }

        var response = AnalyticsMapper.ToResponse(analytics, listing.Title.Value);

        return Result.Success(response with
        {
            Daily = [.. response.Daily.Where(day => day.Day >= from)]
        });
    }
}
