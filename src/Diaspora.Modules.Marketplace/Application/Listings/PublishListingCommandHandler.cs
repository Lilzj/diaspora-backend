using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Extensibility.Fraud;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Publishes a listing, once it can answer everything publication requires.
/// <para>
/// <b>Three gates, in order of how much they cost the seller to fix.</b> The aggregate's own
/// publishable check first — a title, a body, a category, a country, a price and a photograph.
/// Then the category's required attributes, which the seller can fill in. Then the fraud providers,
/// which the seller cannot see and cannot argue with, which is why a severe finding holds the
/// listing for review rather than refusing it outright.
/// </para>
/// <para>
/// <b>A held listing is published and hidden, not refused.</b> The seller's work is saved, the
/// listing exists, and a moderator decides — which is the difference between a false positive that
/// costs somebody a day and one that costs them their afternoon's typing.
/// </para>
/// </summary>
public sealed class PublishListingCommandHandler
    : ICommandHandler<PublishListingCommand, ListingResponse>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly ListingCategoryResolver _categories;
    private readonly ListingAttributeValidator _attributes;
    private readonly ListingFraudAssessor _fraud;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public PublishListingCommandHandler(
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        ListingCategoryResolver categories,
        ListingAttributeValidator attributes,
        ListingFraudAssessor fraud,
        IOptions<MarketplaceOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _categories = categories;
        _attributes = attributes;
        _fraud = fraud;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ListingResponse>> Handle(
        PublishListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _listings.GetAsync(
            MarketplaceListingId.From(command.ListingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure<ListingResponse>(MarketplaceErrors.ListingNotFound);
        }

        var seller = await _authorization.RequireListingAccessAsync(listing, cancellationToken);

        if (seller.IsFailure)
        {
            return Result.Failure<ListingResponse>(seller.Error);
        }

        var now = _clock.UtcNow;

        if (command.Request.ExpiresAt is { } requested)
        {
            var expiry = listing.SetExpiry(requested, now, baseRevision: null);

            if (expiry.IsFailure)
            {
                return Result.Failure<ListingResponse>(expiry.Error);
            }
        }
        else if (listing.ExpiresAt is null && _options.Value.DefaultListingLifetimeDays > 0)
        {
            // A default lifetime, so a one-off seller who lists something and forgets does not
            // leave it advertised for years. A shop that wants a permanent listing sets no expiry
            // and clears it explicitly.
            listing.SetExpiry(
                now.AddDays(_options.Value.DefaultListingLifetimeDays), now, baseRevision: null);
        }

        var category = await _categories.FindAsync(listing.CategoryKey, cancellationToken);

        if (category is not null)
        {
            var required = await _attributes.ValidateForPublicationAsync(
                listing, category.Path, cancellationToken);

            if (required.IsFailure)
            {
                return Result.Failure<ListingResponse>(required.Error);
            }
        }

        var published = listing.Publish(
            now, _authorization.CallerId, command.Request.ActivateImmediately);

        if (published.IsFailure)
        {
            return Result.Failure<ListingResponse>(published.Error);
        }

        await ApplyFraudAssessmentAsync(listing, seller.Value, now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ListingMapper.ToResponse(
            listing,
            seller.Value,
            now,
            canManage: true,
            _authorization.IsStaff,
            category: category));
    }

    /// <summary>
    /// Runs the fraud providers and acts on what they found.
    /// <para>
    /// Off entirely when the deployment says so, which is the setting for an operator who wants
    /// signals recorded and every decision made by a human. Nothing here refuses the publication:
    /// severe holds it for review, a warning queues it, and both leave the listing saved.
    /// </para>
    /// </summary>
    private async Task ApplyFraudAssessmentAsync(
        MarketplaceListing listing,
        Domain.Sellers.SellerProfile seller,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!_options.Value.EnforceFraudSignals)
        {
            return;
        }

        var activeListings = await _listings.CountActiveBySellerAsync(seller.Id, cancellationToken);

        var assessment = await _fraud.AssessAsync(
            listing, seller, "publish", activeListings, now, cancellationToken);

        if (assessment.Signals.Count == 0)
        {
            return;
        }

        var summary = assessment.Summarize();

        // The findings go into the moderation trail whatever the outcome, because a moderator
        // looking at a held listing needs to know what held it.
        listing.AddModerationNote(
            _guids.NewSequentialGuid(), listing.CreatedByUserId, summary, now);

        if (assessment.ShouldHold)
        {
            listing.Hide(
                "Held for review before going live.", listing.CreatedByUserId, now);
        }
        else if (assessment.ShouldFlag)
        {
            listing.Flag(flaggedBy: null, "Flagged by an automated signal.", now);
        }
    }
}
