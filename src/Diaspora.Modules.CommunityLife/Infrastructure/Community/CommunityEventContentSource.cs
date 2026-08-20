using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Community;

/// <summary>
/// Lets B6 describe and moderate an event that is being discussed, shared or reported in the feed.
/// <para>
/// <b>This is the whole of B12's coupling to Community, and it points one way.</b> B6 holds a typed
/// reference rather than a copy, so an event that is cancelled or hidden stops rendering in every
/// thread that mentions it without B6 knowing anything about events.
/// </para>
/// <para>
/// <b>A deleted or missing event is described rather than omitted.</b> A comment thread about
/// something that has gone needs to say so; returning null would leave B6 rendering an empty card
/// with no explanation, which reads as a bug rather than as a withdrawal.
/// </para>
/// <para>
/// <b>Moderation from the feed is applied to the event itself.</b> A moderator hiding a post about a
/// fraudulent event and leaving the event live would have moved the complaint rather than the
/// problem.
/// </para>
/// </summary>
public sealed class CommunityEventContentSource : ICommunityContentSource
{
    private readonly ICommunityEventRepository _events;
    private readonly IClock _clock;

    public CommunityEventContentSource(ICommunityEventRepository events, IClock clock)
    {
        _events = events;
        _clock = clock;
    }

    public CommunityContentType ContentType => CommunityContentType.Event;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        if (contentId == Guid.Empty)
        {
            return null;
        }

        var communityEvent = await _events.GetIncludingDeletedAsync(
            CommunityEventId.From(contentId), cancellationToken);

        return communityEvent is null ? Missing(contentId) : Describe(communityEvent);
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

        var ids = contentIds.Where(id => id != Guid.Empty).Distinct().ToList();

        // One batched read. B6 batches its own calls, so this is bounded by a page of the feed.
        var events = await _events.FindManyAsync(
            [.. ids.Select(CommunityEventId.From)], cancellationToken);

        foreach (var communityEvent in events)
        {
            described[communityEvent.Id.Value] = Describe(communityEvent);
        }

        // Anything the read did not return has gone. Described rather than dropped, so the feed can
        // say so.
        foreach (var id in ids.Where(id => !described.ContainsKey(id)))
        {
            described[id] = Missing(id);
        }

        return described;
    }

    public async Task ApplyModerationAsync(
        Guid contentId,
        ModerationActionType action,
        CancellationToken cancellationToken = default)
    {
        var communityEvent = await _events.GetIncludingDeletedAsync(
            CommunityEventId.From(contentId), cancellationToken);

        if (communityEvent is null)
        {
            return;
        }

        var now = _clock.UtcNow;

        switch (action)
        {
            case ModerationActionType.Hide:
            case ModerationActionType.Reject:
                communityEvent.Hide("Moderated from the community feed.", null, now);
                break;

            case ModerationActionType.Suspend:
                communityEvent.Suspend("Suspended from the community feed.", null, now);
                break;

            case ModerationActionType.Approve:
            case ModerationActionType.Restore:
                communityEvent.Clear(null, now);
                break;

            case ModerationActionType.Archive:
                communityEvent.Archive(now, null, "Archived from the community feed.");
                break;

            default:
                // Pinning and unpinning are facts about a post, not about the event it mentions.
                return;
        }

        _events.Update(communityEvent);
    }

    private static ContentDescriptor Describe(CommunityEvent communityEvent) =>
        new()
        {
            Reference = ContentReference.Create(
                CommunityContentType.Event, communityEvent.Id.Value),
            AuthorId = communityEvent.OrganizerUserId.Value,
            Title = communityEvent.Title.Value,
            Exists = !communityEvent.IsDeleted
                && communityEvent.Status != EventStatus.Archived,
            Visibility = ToVisibility(communityEvent),
            CountryCode = communityEvent.Location.CountryCode,
            AreaId = communityEvent.Location.NeighborhoodAreaId,

            // An event that has finished stops taking comments. A thread that stays open on a
            // funeral or a cancelled fundraiser is where the worst of a feed collects.
            AcceptsComments = communityEvent.Status
                is EventStatus.Published or EventStatus.InProgress
        };

    private static ContentDescriptor Missing(Guid contentId) =>
        new()
        {
            Reference = ContentReference.Create(CommunityContentType.Event, contentId),
            AuthorId = Guid.Empty,
            Title = string.Empty,
            Exists = false,
            Visibility = VisibilityLevel.Private,
            AcceptsComments = false
        };

    /// <summary>
    /// B12's visibility restated in B6's vocabulary.
    /// <para>
    /// A hidden or suspended event reports private whatever its own visibility says, so moderation
    /// takes effect in the feed without B6 having to know what moderation means here.
    /// </para>
    /// </summary>
    private static VisibilityLevel ToVisibility(CommunityEvent communityEvent)
    {
        if (communityEvent.ModerationState is CommunityLifeModerationState.Hidden
            or CommunityLifeModerationState.Suspended)
        {
            return VisibilityLevel.Private;
        }

        return communityEvent.Visibility switch
        {
            EventVisibility.Public => VisibilityLevel.Public,
            EventVisibility.Unlisted => VisibilityLevel.Public,
            EventVisibility.MembersOnly => VisibilityLevel.Members,
            _ => VisibilityLevel.Private
        };
    }
}
