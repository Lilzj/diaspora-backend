using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Saved;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Modules.Marketplace.Extensibility.Views;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Discovery;

/// <summary>
/// Reads one listing, and counts the view.
/// <para>
/// <b>The viewer-relative fields are why this is one call rather than four.</b> Whether the caller
/// may edit it, whether they have saved it, whether they already have an offer or a reservation
/// standing — each is otherwise a separate round trip, and a client that made four of them would
/// render the page in four stages on a slow connection.
/// </para>
/// <para>
/// <b>The view count is recorded on a read, which is unusual and deliberate.</b> A listing's view
/// count is the seller's most immediate feedback, and deferring it to an event would mean the
/// number a seller refreshes to watch is always a beat behind. It is a counter mutation, so it bumps
/// no revision and cannot make the seller's in-flight edit fail.
/// </para>
/// <para>
/// The provider and guide cards are hydrated from B7 and B8 at read time rather than from copies, so
/// a shop that renamed itself or a guide that was unpublished is never misrepresented by a listing
/// written months ago. Both fail soft: an outage renders bare references, not a failed page.
/// </para>
/// </summary>
[SideEffecting(Reason = "Records the view count the seller watches, on the read that caused it.")]
public sealed class GetListingQueryHandler : IQueryHandler<GetListingQuery, ListingResponse>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly ISavedListingRepository _saved;
    private readonly IListingOfferRepository _offers;
    private readonly IListingReservationRepository _reservations;
    private readonly IListingAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly ListingCategoryResolver _categories;
    private readonly ListingAttributeValidator _attributes;
    private readonly MarketplaceProviderResolver _providers;
    private readonly MarketplaceGuideResolver _guides;
    private readonly IListingViewTracker _views;
    private readonly IClock _clock;

    public GetListingQueryHandler(
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        ISavedListingRepository saved,
        IListingOfferRepository offers,
        IListingReservationRepository reservations,
        IListingAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        ListingCategoryResolver categories,
        ListingAttributeValidator attributes,
        MarketplaceProviderResolver providers,
        MarketplaceGuideResolver guides,
        IListingViewTracker views,
        IClock clock)
    {
        _listings = listings;
        _sellers = sellers;
        _saved = saved;
        _offers = offers;
        _reservations = reservations;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _categories = categories;
        _attributes = attributes;
        _providers = providers;
        _guides = guides;
        _views = views;
        _clock = clock;
    }

    public async Task<Result<ListingResponse>> Handle(
        GetListingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var listing = await ResolveAsync(query, cancellationToken);

        if (listing is null)
        {
            return Result.Failure<ListingResponse>(MarketplaceErrors.ListingNotFound);
        }

        // Not-found rather than forbidden. A 403 on a hidden listing confirms it exists.
        if (!await _authorization.CanSeeAsync(listing, cancellationToken))
        {
            return Result.Failure<ListingResponse>(MarketplaceErrors.ListingNotFound);
        }

        var seller = await _sellers.GetForReadAsync(listing.SellerId, cancellationToken);
        var canManage = await _authorization.OwnsAsync(listing, cancellationToken);

        var category = await _categories.FindAsync(listing.CategoryKey, cancellationToken);

        var definitions = category is null
            ? []
            : await _attributes.GetDefinitionsAsync(category.Path, cancellationToken);

        var providers = await _providers.ResolveAsync(
            [.. listing.ProviderReferences.Select(reference => reference.ProviderId)],
            cancellationToken);

        var guides = await _guides.ResolveAsync(
            [.. listing.GuideReferences.Select(reference => reference.GuideId)],
            cancellationToken);

        var viewer = await ResolveViewerStateAsync(listing, cancellationToken);

        await RecordViewAsync(listing, canManage, query.ViewerKey, cancellationToken);

        return Result.Success(ListingMapper.ToResponse(
            listing,
            seller,
            _clock.UtcNow,
            canManage || _authorization.IsStaff,
            _authorization.IsStaff,
            viewer.IsSaved,
            viewer.OfferId,
            viewer.ReservationId,
            category,
            definitions,
            providers,
            guides));
    }

    private async Task<MarketplaceListing?> ResolveAsync(
        GetListingQuery query, CancellationToken cancellationToken)
    {
        if (query.ListingId is { } id)
        {
            // Tracked, because the view counter mutates it. Every other read on this path is
            // untracked.
            return await _listings.GetAsync(MarketplaceListingId.From(id), cancellationToken);
        }

        var slug = ListingSlug.Create(query.Slug);

        return slug.IsFailure
            ? null
            : await _listings.GetBySlugAsync(slug.Value, cancellationToken);
    }

    /// <summary>
    /// Whether the viewer saved it, and whether they already have a commitment on it.
    /// <para>
    /// All three are null for an anonymous reader, which is what lets the client render a sign-in
    /// prompt rather than a wrong state.
    /// </para>
    /// </summary>
    private async Task<(bool? IsSaved, Guid? OfferId, Guid? ReservationId)>
        ResolveViewerStateAsync(
            MarketplaceListing listing, CancellationToken cancellationToken)
    {
        if (_authorization.CallerId is not { } viewer)
        {
            return (null, null, null);
        }

        var saved = await _saved.FindSavedListingIdsAsync(
            viewer, [listing.Id], cancellationToken);

        var offer = await _offers.GetOpenForBuyerAsync(listing.Id, viewer, cancellationToken);

        var reservation = await _reservations.GetActiveForBuyerAsync(
            listing.Id, viewer, cancellationToken);

        return (saved.Contains(listing.Id.Value), offer?.Id.Value, reservation?.Id.Value);
    }

    /// <summary>
    /// Counts the view, unless the seller is looking at their own listing.
    /// <para>
    /// A seller refreshing their own page all afternoon must not inflate the number they are
    /// refreshing to read — which is both a data-quality point and, more importantly, the reason a
    /// seller can trust it at all.
    /// </para>
    /// </summary>
    private async Task RecordViewAsync(
        MarketplaceListing listing,
        bool isOwner,
        string? viewerKey,
        CancellationToken cancellationToken)
    {
        if (isOwner)
        {
            return;
        }

        var key = _authorization.CallerId?.Value.ToString() ?? viewerKey;

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var unique = await _views.RegisterViewAsync(
            listing.Id.Value, key, cancellationToken);

        listing.RecordView(unique);

        var analytics = await _analytics.GetForListingAsync(listing.Id, cancellationToken);

        analytics?.RecordView(_clock.UtcNow, unique);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
