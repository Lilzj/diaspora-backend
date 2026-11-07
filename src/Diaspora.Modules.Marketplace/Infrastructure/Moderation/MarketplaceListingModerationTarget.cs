using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Infrastructure.Moderation;

/// <summary>
/// A marketplace listing, as a moderation case sees it (7 Nov 2026).
/// <para>
/// <b>The only way staff change a member's listing now.</b> The seller's routes refuse staff, and
/// the marketplace moderation routes that hid, suspended and restored listings and closed reports
/// are gone: a decision is made on the Trust case and carried out here, in the same transaction,
/// with the real moderator recorded (the Community hook this replaces put the listing's creator
/// down as the one who hid it).
/// </para>
/// <para>
/// A hide pending review uses the listing's Hidden state, which says "we are looking at this"; a
/// removal uses Suspended, which says "we looked, and it stays down". Neither archives, deletes
/// or marks the listing sold, so the seller's paid placements and earned points are not touched by
/// the decision itself.
/// </para>
/// </summary>
public sealed class MarketplaceListingModerationTarget : IModerationTarget
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public MarketplaceListingModerationTarget(
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _sellers = sellers;
        _guids = guids;
        _clock = clock;
    }

    public string ContentType => ModerationContentTypes.MarketplaceListing;

    public string Area => ModerationAreas.Marketplace;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var listing = await _listings.GetForReadAsync(
            MarketplaceListingId.From(contentId), cancellationToken);

        if (listing is null)
        {
            return null;
        }

        var seller = await _sellers.GetForReadAsync(listing.SellerId, cancellationToken);

        // The member behind the seller profile is who answers for the listing, and who is told.
        var owner = seller?.OwnerUserId.Value ?? listing.CreatedByUserId.Value;

        return new ModerationTargetSnapshot(
            listing.Id.Value, owner, listing.Title.Value, listing.ModerationState.ToString());
    }

    public async Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // Including a listing its seller has since deleted: the case was opened about it, and a
        // decision must still be possible (and must still hold if the seller restores it).
        var listing = await _listings.GetIncludingDeletedAsync(
            MarketplaceListingId.From(contentId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure<ModerationTargetChange>(Error.NotFound(
                "marketplace.listing.not-found", "The listing this case is about no longer exists."));
        }

        var before = listing.ModerationState;
        var now = _clock.UtcNow;
        UserId? moderator = actor.ModeratorUserId is { } id ? UserId.From(id) : null;

        var applied = outcome switch
        {
            // A hide never weakens a removal that already stands on the listing.
            ModerationOutcome.HidePendingReview => before == ListingModerationState.Suspended
                ? Result.Success()
                : listing.Hide(actor.Note, moderator, now),
            ModerationOutcome.Remove => listing.Suspend(actor.Note, moderator, now),
            ModerationOutcome.Restore => listing.Unhide(moderator, now),

            // Looked at and found to be fine: the automatic flag comes off, nothing stronger.
            ModerationOutcome.NoAction => listing.ClearFlag(moderator, now),
            _ => Result.Failure(Error.Validation(
                "marketplace.moderation.outcome", $"'{outcome}' is not a decision."))
        };

        if (applied.IsFailure)
        {
            return Result.Failure<ModerationTargetChange>(applied.Error);
        }

        // The listing's own editorial trail keeps who decided and why, as the moderation route
        // did. Its author must be a person, so the system's automatic hide is recorded on the case
        // alone; a full trail does not undo a decision the case has already made.
        if (moderator is not null)
        {
            listing.AddModerationNote(
                _guids.NewSequentialGuid(),
                moderator,
                $"{outcome} (case {actor.CaseId}, {actor.ReasonCode}): {actor.Note}",
                now);
        }

        return Result.Success(new ModerationTargetChange(
            before.ToString(), listing.ModerationState.ToString()));
    }
}
