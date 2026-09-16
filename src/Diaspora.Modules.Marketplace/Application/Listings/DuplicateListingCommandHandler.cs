using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Copies a listing into a new draft.
/// <para>
/// <b>Copied:</b> title (prefixed), description, category, listing type, condition, price,
/// location, fulfilment options and product identity — everything a seller would otherwise retype
/// for a near-identical item, which is the case that makes this worth building.
/// </para>
/// <para>
/// <b>Not copied:</b> publication state (always a draft), the deep-link handle (a fresh one is
/// allocated), engagement, the featured flag, moderation state, expiry, and the analytics rollup —
/// the copy gets its own, at zero. Copying a view count would let a seller mint a listing that
/// looks established.
/// </para>
/// <para>
/// <b>Inventory is deliberately reset to one.</b> Quantity is the field most likely to be wrong on
/// a copy: a seller duplicating a sold-out listing of twelve does not have twelve more. One is the
/// safe default and the seller sets it before publishing.
/// </para>
/// <para>
/// <b>The active-listing ceiling applies</b>, for the same reason it applies to create: it bounds
/// rows a script can make for free, and a duplicate route is exactly such a script.
/// </para>
/// </summary>
public sealed class DuplicateListingCommandHandler
    : ICommandHandler<DuplicateListingCommand, ListingResponse>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IListingAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly ListingCategoryResolver _categories;
    private readonly ListingSlugAllocator _slugs;
    private readonly ListingAreaFiling _filing;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public DuplicateListingCommandHandler(
        IMarketplaceListingRepository listings,
        IListingAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        ListingCategoryResolver categories,
        ListingSlugAllocator slugs,
        ListingAreaFiling filing,
        IOptions<MarketplaceOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _categories = categories;
        _slugs = slugs;
        _filing = filing;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ListingResponse>> Handle(
        DuplicateListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var seller = await _authorization.RequireSellerAsync(cancellationToken);

        if (seller.IsFailure)
        {
            return Result.Failure<ListingResponse>(seller.Error);
        }

        var source = await _listings.GetAsync(
            MarketplaceListingId.From(command.ListingId), cancellationToken);

        if (source is null)
        {
            return Result.Failure<ListingResponse>(MarketplaceErrors.ListingNotFound);
        }

        // The caller's own seller profile, not merely a seller. Otherwise any seller could copy any
        // other seller's listing wholesale, which is the shape of a marketplace scraping problem.
        if (source.SellerId != seller.Value.Id)
        {
            return Result.Failure<ListingResponse>(MarketplaceErrors.NotListingOwner);
        }

        var openCount = await _listings.CountActiveBySellerAsync(
            seller.Value.Id, cancellationToken);

        if (openCount >= _options.Value.MaxActiveListingsPerSeller)
        {
            return Result.Failure<ListingResponse>(MarketplaceErrors.TooManyListings);
        }

        var title = ListingTitle.Create(
            string.IsNullOrWhiteSpace(command.Title)
                ? Copied(source.Title.Value)
                : command.Title);

        if (title.IsFailure)
        {
            return Result.Failure<ListingResponse>(title.Error);
        }

        // The create path refuses an exact title repeat as a double-submit. A copy carries the
        // prefix so it does not trip that, but an explicitly supplied title can, and the same rule
        // should apply: two live listings with one name is the seller's mistake either way.
        if (await _listings.HasDuplicateAsync(
                seller.Value.Id, title.Value.Value, excluding: null, cancellationToken))
        {
            return Result.Failure<ListingResponse>(MarketplaceErrors.DuplicateListing);
        }

        // Re-resolved rather than trusted from the source: a category retired since the original
        // was written must not be inherited into a new draft the seller then cannot publish.
        var category = await _categories.ResolveActiveAsync(source.CategoryKey, cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<ListingResponse>(category.Error);
        }

        var slug = await _slugs.AllocateListingSlugAsync(
            null, title.Value.Value, source.Location.CityName, cancellationToken);

        if (slug.IsFailure)
        {
            return Result.Failure<ListingResponse>(slug.Error);
        }

        // ── Every value object is rebuilt, and this is the whole of the bug ──
        //
        // The first version passed `source.Description`, `source.Price`,
        // `source.Location`, `source.Fulfillment` and `source.Product` straight
        // into the copy. Every one of them is an `OwnsOne` — an *owned entity*
        // with an identifying foreign key back to its listing — so handing the
        // source's tracked instance to a second aggregate asks EF to re-parent
        // it, and it refuses:
        //
        //   The property 'ListingDescription.MarketplaceListingId' is part of a
        //   key and so cannot be modified or marked as modified.
        //
        // 500, on the member's first press of Duplicate.
        //
        // A value object that is `OwnsOne` cannot be shared between aggregates,
        // only re-created from its primitives — which is what these do. The
        // factories also re-validate, so a copy cannot reach a state a create
        // could not.
        var description = ListingDescription.Create(source.Description.Body, source.Description.Summary);

        if (description.IsFailure)
        {
            return Result.Failure<ListingResponse>(description.Error);
        }

        var price = ListingPrice.Create(
            source.Price.Amount, source.Price.CurrencyCode,
            source.Price.IsNegotiable, source.Price.MinimumOffer);

        if (price.IsFailure)
        {
            return Result.Failure<ListingResponse>(price.Error);
        }

        var location = ListingLocationContext.Create(
            source.Location.CountryCode, source.Location.RegionName, source.Location.CityName,
            source.Location.Neighbourhood, source.Location.AreaId, source.Location.AreaPath);

        if (location.IsFailure)
        {
            return Result.Failure<ListingResponse>(location.Error);
        }

        // A copy of a listing written before 16 Sep 2026 is filed now, rather than inheriting
        // "nowhere" from its original.
        var placed = await _filing.FileAsync(location.Value, cancellationToken);

        var fulfillment = FulfillmentOptions.Create(
            source.Fulfillment.OffersPickup, source.Fulfillment.OffersDelivery,
            source.Fulfillment.OffersShipping, source.Fulfillment.IsDigital,
            source.Fulfillment.DeliveryRadiusKm, source.Fulfillment.Notes);

        if (fulfillment.IsFailure)
        {
            return Result.Failure<ListingResponse>(fulfillment.Error);
        }

        var product = ProductIdentity.Create(
            source.Product.Brand, source.Product.Model, source.Product.Sku, source.Product.Year);

        if (product.IsFailure)
        {
            return Result.Failure<ListingResponse>(product.Error);
        }

        var now = _clock.UtcNow;

        var copy = MarketplaceListing.Create(
            MarketplaceListingId.From(_guids.NewSequentialGuid()),
            seller.Value.Id,
            _authorization.CallerId!,
            title.Value,
            slug.Value,
            description.Value,
            category.Value.Key,
            source.ListingType,
            source.Condition,
            price.Value,
            placed,
            now);

        if (copy.IsFailure)
        {
            return Result.Failure<ListingResponse>(copy.Error);
        }

        // Fulfilment, product identity, contact preference and visibility carried across in one
        // call — the same mutator the create path uses, so a copy cannot reach a state a create
        // could not.
        var details = copy.Value.UpdateDetails(
            copy.Value.Title,
            description.Value,
            source.Condition,
            price.Value,
            placed,
            fulfillment.Value,
            product.Value,
            source.ContactPreference,
            source.Visibility,
            baseRevision: null,
            _authorization.CallerId);

        if (details.IsFailure)
        {
            return Result.Failure<ListingResponse>(details.Error);
        }

        await _listings.AddAsync(copy.Value, cancellationToken);

        // Its own rollup, at zero. See the class note on why engagement is not carried over, and
        // CreateListingCommandHandler on why the row is made here rather than lazily.
        await _analytics.AddAsync(
            ListingAnalytics.Create(
                ListingAnalyticsId.From(_guids.NewSequentialGuid()),
                copy.Value.Id,
                seller.Value.Id,
                category.Value.Key,
                source.Location.CountryCode,
                now),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ListingMapper.ToResponse(
            copy.Value, seller.Value, now, canManage: true, category: category.Value));
    }

    /// <summary>Prefixes once. See DuplicateGuideCommandHandler for why.</summary>
    private static string Copied(string title) =>
        title.StartsWith("Copy of ", StringComparison.OrdinalIgnoreCase)
            ? title
            : $"Copy of {title}";
}
