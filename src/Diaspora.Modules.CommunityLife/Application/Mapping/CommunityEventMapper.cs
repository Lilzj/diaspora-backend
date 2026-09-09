using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Modules.CommunityLife.Integration;

namespace Diaspora.Modules.CommunityLife.Application.Mapping;

/// <summary>
/// Turns an event into the shapes the wire and the platform expect.
/// <para>
/// <b>The checked-in count is rendered only to organizers.</b> A live attendance figure at a door is
/// operationally useful to whoever is running the event and is nobody else's business — an event
/// where "seventeen of forty arrived" is public is one where a member can be inferred absent.
/// </para>
/// <para>
/// <b>Verification evidence is never in a media list.</b> It is attached to the same aggregate as
/// the gallery and filtered out here, so a constitution submitted to Trust cannot appear in a
/// public carousel.
/// </para>
/// </summary>
public static class CommunityEventMapper
{
    public static EventResponse ToResponse(
        CommunityEvent communityEvent,
        EventCategory? category = null,
        string? organizationName = null,
        string? organizationSlug = null,
        bool isOrganizerVerified = false,
        EventRsvp? viewerRsvp = null,
        bool viewerIsOrganizer = false,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        var moment = now ?? DateTimeOffset.UtcNow;

        return new EventResponse
        {
            EventId = communityEvent.Id.Value,
            Title = communityEvent.Title.Value,
            Slug = communityEvent.Slug.Value,
            CategoryKey = communityEvent.CategoryKey,
            CategoryName = category?.Name,
            Facets = CommunityLifeSharedMapper.SplitFacets(category?.Facets),
            Summary = communityEvent.Description.Summary,
            Description = communityEvent.Description.Body,
            OrganizationId = communityEvent.OrganizationId?.Value,
            OrganizationName = organizationName,
            OrganizationSlug = organizationSlug,
            OrganizerUserId = communityEvent.OrganizerUserId.Value,
            HostName = communityEvent.HostName,
            IsOrganizerVerified = isOrganizerVerified,
            Schedule = new EventScheduleResponse
            {
                StartsAt = communityEvent.Schedule.StartsAt,
                EndsAt = communityEvent.Schedule.EndsAt,
                TimeZoneId = communityEvent.Schedule.TimeZoneId,
                RegistrationDeadline = communityEvent.Schedule.RegistrationDeadline,
                CheckInOpensAt = communityEvent.Schedule.CheckInOpensAt
            },
            AttendanceMode = communityEvent.AttendanceMode.ToString(),
            Location = CommunityLifeSharedMapper.ToResponse(communityEvent.Location),
            Venue = new EventVenueResponse
            {
                Name = communityEvent.Venue.Name,
                AddressLine1 = communityEvent.Venue.AddressLine1,
                AddressLine2 = communityEvent.Venue.AddressLine2,
                Landmark = communityEvent.Venue.Landmark,
                PostalCode = communityEvent.Venue.PostalCode,
                Latitude = communityEvent.Venue.Latitude,
                Longitude = communityEvent.Venue.Longitude,
                OnlineUrl = communityEvent.Venue.OnlineUrl
            },
            Capacity = ToResponse(communityEvent, viewerIsOrganizer),
            Contact = CommunityLifeSharedMapper.ToResponse(communityEvent.Contact),
            Agenda =
            [
                .. communityEvent.AgendaItems.Select(item => new AgendaItemResponse
                {
                    Id = item.Id,
                    Title = item.Title,
                    Description = item.Description,
                    StartOffsetMinutes = item.StartOffsetMinutes,
                    StartsAt = item.StartsAt(communityEvent.Schedule.StartsAt),
                    DurationMinutes = item.DurationMinutes,
                    SpeakerName = item.SpeakerName,
                    Order = item.Order
                })
            ],
            Speakers =
            [
                .. communityEvent.Speakers.Select(speaker => new EventSpeakerResponse
                {
                    Id = speaker.Id,
                    Name = speaker.Name,
                    Title = speaker.Title,
                    Organization = speaker.Organization,
                    Biography = speaker.Biography,
                    PhotoMediaId = speaker.PhotoMediaId,
                    MemberUserId = speaker.MemberUserId,
                    Order = speaker.Order
                })
            ],
            Media =
            [
                .. communityEvent.MediaReferences
                    .Where(media => media.Role != CommunityLifeMediaRole.VerificationEvidence)
                    .Select(media => new MediaReferenceResponse
                    {
                        MediaId = media.MediaId,
                        Role = media.Role.ToString(),
                        Order = media.Order,
                        Caption = media.Caption
                    })
            ],
            Guides =
            [
                .. communityEvent.GuideReferences.Select(guide => new GuideReferenceResponse
                {
                    GuideId = guide.GuideId,
                    Title = guide.Title,
                    Slug = guide.Slug
                })
            ],
            Providers =
            [
                .. communityEvent.ProviderReferences.Select(provider =>
                    new ProviderReferenceResponse
                    {
                        ProviderId = provider.ProviderId,
                        ProviderName = provider.ProviderName,
                        Role = provider.Role
                    })
            ],
            LinkedResources =
            [
                .. communityEvent.LinkedResources.Select(resource =>
                    new LinkedResourceResponse
                    {
                        ResourceType = resource.ResourceType.ToString(),
                        ResourceId = resource.ResourceId == Guid.Empty
                            ? null
                            : resource.ResourceId,
                        Label = resource.Label,
                        Url = resource.Url,
                        Order = resource.Order
                    })
            ],
            CoverMediaId = communityEvent.CoverImage?.MediaId,
            Status = communityEvent.Status.ToString(),
            Visibility = communityEvent.Visibility.ToString(),
            ModerationState = communityEvent.ModerationState.ToString(),
            RequiresRsvp = communityEvent.RequiresRsvp,
            AcceptsRsvp = communityEvent.AcceptsRsvpAt(moment),
            AcceptsCheckIn = communityEvent.AcceptsCheckInAt(moment),
            IsAttendeeListPublic = communityEvent.IsAttendeeListPublic,
            IsFree = communityEvent.Audience.IsFree,
            IsFamilyFriendly = communityEvent.Audience.IsFamilyFriendly,
            IsChildFriendly = communityEvent.Audience.IsChildFriendly,
            IsStudentFocused = communityEvent.Audience.IsStudentFocused,
            DressCode = communityEvent.Audience.DressCode,
            AccessibilityNotes = communityEvent.Audience.AccessibilityNotes,
            IsFeatured = communityEvent.IsFeatured,
            CancellationReason = communityEvent.CancellationReason,
            ViewerRsvpStatus = viewerRsvp?.Status.ToString(),
            ViewerRsvpId = viewerRsvp?.Id.Value,
            ViewCount = communityEvent.Engagement.ViewCount,
            Revision = communityEvent.Revision,
            PublishedAt = communityEvent.PublishedAt,
            CreatedAt = communityEvent.CreatedAt,
            SeriesId = communityEvent.SeriesId?.Value,
            SeriesSequence = communityEvent.SeriesSequence,

            // Null on a one-off occasion rather than "Scheduled", because a single event has no
            // occurrence status to have and reporting one would invite a client to render "this
            // date is on as planned" beside something that was never part of a pattern.
            OccurrenceStatus = communityEvent.IsOccurrence
                ? OccurrenceStatusProjection.For(communityEvent).ToString()
                : null,
            OriginalStartsAt = communityEvent.OriginalStartsAt
        };
    }

    /// <summary>The counts. The checked-in figure is organizers only — see the class summary.</summary>
    public static EventCapacityResponse ToResponse(
        CommunityEvent communityEvent, bool viewerIsOrganizer)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        return new EventCapacityResponse
        {
            Maximum = communityEvent.Capacity.Maximum,
            GoingCount = communityEvent.Capacity.GoingCount,
            InterestedCount = communityEvent.Capacity.InterestedCount,
            WaitlistCount = communityEvent.Capacity.WaitlistCount,
            CheckedInCount = viewerIsOrganizer ? communityEvent.Capacity.CheckedInCount : null,
            RemainingPlaces = communityEvent.Capacity.RemainingPlaces,
            IsFull = communityEvent.Capacity.IsFull
        };
    }

    public static EventSummaryResponse ToSummary(
        CommunityEvent communityEvent,
        string? organizationName = null,
        bool isOrganizerVerified = false,
        double? distanceKm = null,
        string? viewerRsvpStatus = null)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        return new EventSummaryResponse
        {
            EventId = communityEvent.Id.Value,
            Title = communityEvent.Title.Value,
            Slug = communityEvent.Slug.Value,
            CategoryKey = communityEvent.CategoryKey,
            Summary = communityEvent.Description.Summary,
            OrganizationId = communityEvent.OrganizationId?.Value,
            OrganizationName = organizationName,
            StartsAt = communityEvent.Schedule.StartsAt,
            EndsAt = communityEvent.Schedule.EndsAt,
            TimeZoneId = communityEvent.Schedule.TimeZoneId,
            AttendanceMode = communityEvent.AttendanceMode.ToString(),
            VenueName = communityEvent.Venue.Name,
            Location = CommunityLifeSharedMapper.ToResponse(communityEvent.Location),
            CoverMediaId = communityEvent.CoverImage?.MediaId,
            IsFree = communityEvent.Audience.IsFree,
            IsFamilyFriendly = communityEvent.Audience.IsFamilyFriendly,
            IsFeatured = communityEvent.IsFeatured,
            IsOrganizerVerified = isOrganizerVerified,
            GoingCount = communityEvent.Capacity.GoingCount,
            RemainingPlaces = communityEvent.Capacity.RemainingPlaces,
            Status = communityEvent.Status.ToString(),
            DistanceKm = distanceKm,
            ViewerRsvpStatus = viewerRsvpStatus,
            SeriesId = communityEvent.SeriesId?.Value,
            IsRecurring = communityEvent.IsOccurrence
        };
    }

    /// <summary>The published contract other modules consume. Flat primitives, and no attendee.</summary>
    public static EventSummary ToPublished(
        CommunityEvent communityEvent,
        string? organizationName = null,
        bool isOrganizerVerified = false)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        return new EventSummary
        {
            EventId = communityEvent.Id.Value,
            Title = communityEvent.Title.Value,
            Slug = communityEvent.Slug.Value,
            CategoryKey = communityEvent.CategoryKey,
            Summary = communityEvent.Description.Summary,
            OrganizationId = communityEvent.OrganizationId?.Value,
            OrganizationName = organizationName,
            OrganizerUserId = communityEvent.OrganizerUserId.Value,
            StartsAt = communityEvent.Schedule.StartsAt,
            EndsAt = communityEvent.Schedule.EndsAt,
            TimeZoneId = communityEvent.Schedule.TimeZoneId,
            CountryCode = communityEvent.Location.CountryCode,
            CityName = communityEvent.Location.CityName,
            VenueName = communityEvent.Venue.Name,
            NeighborhoodAreaId = communityEvent.Location.NeighborhoodAreaId,
            Latitude = communityEvent.Venue.Latitude,
            Longitude = communityEvent.Venue.Longitude,
            CoverMediaId = communityEvent.CoverImage?.MediaId,
            IsFree = communityEvent.Audience.IsFree,
            IsFamilyFriendly = communityEvent.Audience.IsFamilyFriendly,
            IsFeatured = communityEvent.IsFeatured,
            IsOrganizerVerified = isOrganizerVerified,
            GoingCount = communityEvent.Capacity.GoingCount,
            RemainingPlaces = communityEvent.Capacity.RemainingPlaces,
            Status = communityEvent.Status.ToString(),
            AttendanceMode = communityEvent.AttendanceMode.ToString()
        };
    }

    /// <param name="waitlistPosition">
    /// Where in the queue a waitlisted response sits. Supplied by the caller rather than derived
    /// here: it is a fact about every other response to the event, and a mapper has no repository.
    /// </param>
    /// <param name="seriesId">
    /// The repeating event the response's date belongs to. Supplied for the same reason — the RSVP
    /// aggregate deliberately holds no reference to a series, because a response is to a date.
    /// </param>
    public static RsvpResponse ToResponse(
        EventRsvp rsvp, int? waitlistPosition = null, Guid? seriesId = null)
    {
        ArgumentNullException.ThrowIfNull(rsvp);

        return new RsvpResponse
        {
            WaitlistPosition = rsvp.Status == RsvpStatus.Waitlisted ? waitlistPosition : null,
            SeriesId = seriesId,
            RsvpId = rsvp.Id.Value,
            EventId = rsvp.EventId.Value,
            OrganizationId = rsvp.OrganizationId?.Value,
            AttendeeUserId = rsvp.AttendeeUserId.Value,
            Status = rsvp.Status.ToString(),
            PartySize = rsvp.PartySize,
            Note = rsvp.Note,
            DeclineReason = rsvp.DeclineReason.ToString(),
            RespondedAt = rsvp.RespondedAt,
            CheckedInAt = rsvp.CheckedInAt,
            CheckInMethod = rsvp.CheckInMethod?.ToString(),
            Revision = rsvp.Revision
        };
    }

    /// <summary>
    /// An attendee row for the organizer's list.
    /// <para>
    /// The note is included, and that is the reason this shape exists separately from
    /// <see cref="RsvpResponse"/>: it is written by an attendee for the organizer, and the endpoint
    /// that serves this is behind the check-in permission.
    /// </para>
    /// </summary>
    public static AttendeeResponse ToAttendee(EventRsvp rsvp, string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(rsvp);

        return new AttendeeResponse
        {
            RsvpId = rsvp.Id.Value,
            AttendeeUserId = rsvp.AttendeeUserId.Value,
            DisplayName = displayName,
            Status = rsvp.Status.ToString(),
            PartySize = rsvp.PartySize,
            Note = rsvp.Note,
            RespondedAt = rsvp.RespondedAt,
            CheckedInAt = rsvp.CheckedInAt
        };
    }
}
