using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Infrastructure.Moderation;

/// <summary>
/// A housing listing, as a moderation case sees it (7 Nov 2026).
/// <para>
/// <b>The only way staff change a member's property now.</b> The landlord's routes refuse staff,
/// and the housing moderation routes that hid, suspended, cleared and archived properties and
/// resolved reports are gone: a decision is made on the Trust case and carried out here, in the
/// same transaction, with the real moderator recorded (the Community hook this replaces put the
/// listing's creator down as the one who hid it).
/// </para>
/// <para>
/// A hide pending review uses the property's Hidden state, which the landlord can see and fix; a
/// removal uses Suspended, which bars republication. Neither archives, deletes or marks the
/// property let, so the landlord's paid placements and earned points are not touched by the
/// decision itself.
/// </para>
/// </summary>
public sealed class HousingListingModerationTarget : IModerationTarget
{
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public HousingListingModerationTarget(
        IHousingListingRepository listings,
        ILandlordProfileRepository landlords,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _landlords = landlords;
        _guids = guids;
        _clock = clock;
    }

    public string ContentType => ModerationContentTypes.HousingListing;

    public string Area => ModerationAreas.Housing;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var listing = await _listings.GetForReadAsync(
            HousingListingId.From(contentId), cancellationToken);

        if (listing is null)
        {
            return null;
        }

        var landlord = await _landlords.GetForReadAsync(listing.LandlordId, cancellationToken);

        // The member behind the landlord profile is who answers for the property, and who is told.
        var owner = landlord?.OwnerUserId.Value ?? listing.CreatedByUserId.Value;

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

        // Including a property its landlord has since deleted: the case was opened about it, and a
        // decision must still be possible (and must still hold if the landlord restores it).
        var listing = await _listings.GetIncludingDeletedAsync(
            HousingListingId.From(contentId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure<ModerationTargetChange>(Error.NotFound(
                "housing.listing.not-found", "The property this case is about no longer exists."));
        }

        var before = listing.ModerationState;
        var now = _clock.UtcNow;
        UserId? moderator = actor.ModeratorUserId is { } id ? UserId.From(id) : null;

        switch (outcome)
        {
            case ModerationOutcome.HidePendingReview:
                // A hide never weakens a removal that already stands on the property.
                if (before != HousingModerationState.Suspended)
                {
                    listing.Hide(actor.Note, moderator, now);
                }

                break;

            case ModerationOutcome.Remove:
                listing.Suspend(actor.Note, moderator, now);
                break;

            case ModerationOutcome.Restore:
                // Lifts a hide or a suspension. A flag is the reports' business, not a restore's.
                if (before is HousingModerationState.Hidden or HousingModerationState.Suspended)
                {
                    listing.Clear(moderator, now);
                }

                break;

            case ModerationOutcome.NoAction:
                // Looked at and found to be fine: the automatic flag comes off, nothing stronger.
                listing.ClearFlag(moderator, now);
                break;

            default:
                return Result.Failure<ModerationTargetChange>(Error.Validation(
                    "housing.moderation.outcome", $"'{outcome}' is not a decision."));
        }

        // The property's own editorial trail keeps who decided and why, as the moderation route
        // did. Its author must be a person, so the system's automatic hide is recorded on the case
        // alone.
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
