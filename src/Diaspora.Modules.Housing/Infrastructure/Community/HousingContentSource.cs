using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;

namespace Diaspora.Modules.Housing.Infrastructure.Community;

/// <summary>
/// Makes properties commentable, reactable, reportable and bookmarkable, through B6.
/// <para>
/// <b>This one class is the whole of what B11 pays to get Community's social surface.</b>
/// <see cref="CommunityContentType.Housing"/> has existed in B6's own enum since it shipped, so the
/// type is claimed rather than invented. Registering it means questions under a listing, reactions,
/// community reports and bookmarks all work, and B11 wrote no social code at all.
/// </para>
/// <para>
/// <b>"Is it still available?" is the single most-asked question on any housing listing</b>, and it
/// is exactly what a comment thread is for. Building a bespoke enquiry feature would have meant
/// reimplementing moderation, reporting and notification for it.
/// </para>
/// <para>
/// <b>Community's reports and this module's are different things, deliberately.</b> A B6 report is
/// about content — an abusive question under a listing. A <c>HousingReport</c> is about a property:
/// the advert is a scam, the flat is already let, the photographs are of somewhere else. They are
/// triaged by different people against different evidence, and one queue would bury the rental
/// fraud under fifty rude comments.
/// </para>
/// <para>
/// <b>Visibility is narrowed, never widened.</b> B6 takes the descriptor's audience as a ceiling,
/// so an unlisted or draft property must not be described as public — that would let a comment on
/// it surface in a feed with no business showing it.
/// </para>
/// </summary>
public sealed class HousingContentSource : ICommunityContentSource
{
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;

    public HousingContentSource(
        IHousingListingRepository listings, ILandlordProfileRepository landlords)
    {
        _listings = listings;
        _landlords = landlords;
    }

    public CommunityContentType ContentType => CommunityContentType.Housing;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var listing = await _listings.GetForReadAsync(
            HousingListingId.From(contentId), cancellationToken);

        if (listing is null)
        {
            return null;
        }

        var landlord = await _landlords.GetForReadAsync(listing.LandlordId, cancellationToken);

        return Describe(listing, landlord);
    }

    public async Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        var described = new Dictionary<Guid, ContentDescriptor>();

        if (contentIds.Count == 0)
        {
            return described;
        }

        // One batched read for the listings and one for their landlords, rather than a pair per
        // identifier. Community batches its own calls, so this is bounded by a page.
        var listings = await _listings.GetManyAsync(
            [.. contentIds.Distinct().Select(HousingListingId.From)], cancellationToken);

        var landlords = await _landlords.GetManyAsync(
            [.. listings.Select(listing => listing.LandlordId).Distinct()], cancellationToken);

        foreach (var listing in listings)
        {
            described[listing.Id.Value] =
                Describe(listing, landlords.GetValueOrDefault(listing.LandlordId));
        }

        return described;
    }

    /// <summary>
    /// Applies a moderation outcome to the property itself.
    /// <para>
    /// Optional on the interface, and implemented here because a moderator hiding a listing's
    /// questions and leaving the listing in discovery is a half-measure that satisfies nobody — and
    /// on a housing platform it leaves a possible rental fraud advertised. Community records the
    /// decision and publishes its event either way; this is B11 acting on its own row, which only
    /// B11 can do.
    /// </para>
    /// <para>
    /// The caller is Community's own moderation pipeline, which owns the unit of work — this
    /// mutates the aggregate and does not save.
    /// </para>
    /// </summary>
    public async Task ApplyModerationAsync(
        Guid contentId,
        ModerationActionType action,
        CancellationToken cancellationToken = default)
    {
        var listing = await _listings.GetAsync(
            HousingListingId.From(contentId), cancellationToken);

        if (listing is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        switch (action)
        {
            case ModerationActionType.Hide:
                listing.Hide("Hidden following moderation.", listing.CreatedByUserId, now);
                break;

            case ModerationActionType.Suspend:
                listing.Suspend("Suspended following moderation.", listing.CreatedByUserId, now);
                break;

            case ModerationActionType.Restore:
            case ModerationActionType.Approve:
                listing.Clear(null, now);
                break;

            case ModerationActionType.Archive:
            case ModerationActionType.Reject:
                listing.Archive(now, null, "Archived following moderation.");
                break;

            default:
                // Pin, Unpin and Escalate say nothing about a property's lifecycle. Community has
                // already recorded them; there is nothing for B11 to do.
                break;
        }
    }

    private static ContentDescriptor Describe(
        HousingListing listing, LandlordProfile? landlord) => new()
    {
        Reference = ContentReference.Create(
            CommunityContentType.Housing, listing.Id.Value),

        // The member behind the landlord profile, because Community's model is about people. The
        // property belongs to the profile; the author of the content is whoever owns it.
        AuthorId = landlord?.OwnerUserId.Value ?? listing.CreatedByUserId.Value,
        Title = listing.Title.Value,
        Exists = !listing.IsDeleted,
        Visibility = ToCommunityVisibility(listing),
        CountryCode = listing.Location.CountryCode,
        AreaId = listing.Location.NeighborhoodAreaId,

        // An occupied or withdrawn property stops taking questions: an answer nobody will act on is
        // worse than no comment box. A live one takes them, because "is it still available" is the
        // single most common thing a member wants to ask.
        AcceptsComments = HousingListingStatusTransitions.IsPubliclyVisible(listing.Status)
            && listing.Status != HousingListingStatus.Occupied
            && listing.ModerationState is HousingModerationState.Clear
                or HousingModerationState.Flagged
    };

    /// <summary>
    /// Projects B11's audience onto B6's, narrowing wherever the two do not line up.
    /// <para>
    /// B6's vocabulary has no <c>Unlisted</c>, so a link-shared property maps to its members-only
    /// band rather than to <c>Public</c>. Erring outward here would surface an unpublished
    /// property's questions in a public feed, which is a privacy failure rather than a rendering
    /// bug — and a draft is private regardless of what its visibility field says.
    /// </para>
    /// </summary>
    private static VisibilityLevel ToCommunityVisibility(HousingListing listing)
    {
        if (!HousingListingStatusTransitions.IsPubliclyVisible(listing.Status))
        {
            return VisibilityLevel.Private;
        }

        return listing.Visibility switch
        {
            HousingVisibility.Public => VisibilityLevel.Public,
            HousingVisibility.Unlisted => VisibilityLevel.Members,
            _ => VisibilityLevel.Private
        };
    }
}
