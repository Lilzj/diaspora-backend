using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Rsvps;

/// <summary>
/// Who is coming to an event.
/// <para>
/// <b>Closed unless the organizer opened it.</b> Who is attending a support meeting, a legal advice
/// session or a political gathering is among the most consequential facts this module holds about a
/// person, and an event page is a link anybody can forward. The organizer sees it either way.
/// </para>
/// <para>
/// <b>Non-organizers see only who is coming.</b> Withdrawals and no-shows are between the attendee
/// and the organizer — a public list that showed them would tell the room who pulled out and who did
/// not turn up.
/// </para>
/// </summary>
public sealed class ListAttendeesQueryHandler
    : IQueryHandler<ListAttendeesQuery, AttendeePageResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IEventRsvpRepository _rsvps;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IMemberProfileService _members;

    public ListAttendeesQueryHandler(
        ICommunityEventRepository events,
        IEventRsvpRepository rsvps,
        CommunityLifeAuthorizationService authorization,
        IMemberProfileService members)
    {
        _events = events;
        _rsvps = rsvps;
        _authorization = authorization;
        _members = members;
    }

    public async Task<Result<AttendeePageResponse>> Handle(
        ListAttendeesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var eventId = CommunityEventId.From(query.EventId);

        var communityEvent = await _events.GetAsync(eventId, cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure<AttendeePageResponse>(CommunityLifeErrors.EventNotFound);
        }

        var readable = await _authorization.RequireEventReadAsync(
            communityEvent, cancellationToken);

        if (readable.IsFailure)
        {
            return Result.Failure<AttendeePageResponse>(readable.Error);
        }

        var organizer = await _authorization.RequireEventWriteAsync(
            communityEvent, cancellationToken);

        var isOrganizer = organizer.IsSuccess;

        if (!isOrganizer && !communityEvent.IsAttendeeListPublic)
        {
            return Result.Failure<AttendeePageResponse>(
                CommunityLifeErrors.AttendeeListPrivate);
        }

        var page = await _rsvps.SearchAsync(
            new RsvpQueryFilter
            {
                EventId = eventId,
                Statuses = ResolveStatuses(query, isOrganizer),
                Cursor = query.Cursor,
                Limit = Math.Clamp(query.Limit ?? 20, 1, CommunityLifeLimits.MaxPageSize)
            },
            cancellationToken);

        // **The list had no names on it.** An attendee came back as an identifier, a status and a
        // party size, so an organizer checking people in at a door was reading rows of nothing.
        // One call for the page, through the same member port that named the people in a
        // conversation — not one lookup per row.
        var names = await _members.ResolveAsync(
            [.. page.Items.Select(rsvp => rsvp.AttendeeUserId)], cancellationToken);

        return Result.Success(new AttendeePageResponse
        {
            Items =
            [
                .. page.Items.Select(rsvp => CommunityEventMapper.ToAttendee(
                    rsvp,
                    names.TryGetValue(rsvp.AttendeeUserId.Value, out var profile)
                        ? profile.DisplayName
                        : null))
            ],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            GoingCount = communityEvent.Capacity.GoingCount,
            RemainingPlaces = communityEvent.Capacity.RemainingPlaces
        });
    }

    /// <summary>
    /// The statuses this caller may see, whatever they asked for.
    /// <para>
    /// Narrowed rather than refused, so a client that sends the organizer's filter gets the public
    /// answer instead of an error it cannot act on.
    /// </para>
    /// </summary>
    private static List<RsvpStatus> ResolveStatuses(ListAttendeesQuery query, bool isOrganizer)
    {
        List<RsvpStatus> visible = isOrganizer
            ?
            [
                RsvpStatus.Going,
                RsvpStatus.Interested,
                RsvpStatus.Waitlisted,
                RsvpStatus.CheckedIn,
                RsvpStatus.Attended,
                RsvpStatus.Cancelled,
                RsvpStatus.NoShow
            ]
            : [RsvpStatus.Going, RsvpStatus.CheckedIn, RsvpStatus.Attended];

        if (query.Statuses is not { Count: > 0 })
        {
            return visible;
        }

        var asked = query.Statuses
            .Select(CommunityLifeSharedMapper.ParseEnum<RsvpStatus>)
            .OfType<RsvpStatus>()
            .Where(visible.Contains)
            .Distinct()
            .ToList();

        return asked.Count == 0 ? visible : asked;
    }
}
