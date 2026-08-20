using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;

namespace Diaspora.Modules.CommunityLife.Application.Mapping;

/// <summary>
/// Turns a series and its dates into the shapes the wire expects.
/// <para>
/// <b>The rule always travels with its description.</b> The server owns the expansion semantics, so
/// the server owns the sentence describing them; a client composing its own would drift from the
/// rules the first time either changed, and would drift silently.
/// </para>
/// <para>
/// <b>An occurrence's identifier is its event's identifier.</b> Nothing is renamed in transit —
/// a client holding an <c>occurrenceId</c> can call <c>/events/{id}</c> with it, RSVP against it,
/// and read its attendees, because it is the same row.
/// </para>
/// </summary>
public static class EventSeriesMapper
{
    public static EventSeriesResponse ToResponse(
        EventSeries series,
        EventCategory? category = null,
        string? organizationName = null,
        string? organizationSlug = null,
        bool isOrganizerVerified = false,
        EventOccurrenceResponse? nextOccurrence = null)
    {
        ArgumentNullException.ThrowIfNull(series);

        return new EventSeriesResponse
        {
            SeriesId = series.Id.Value,
            Title = series.Title.Value,
            Slug = series.Slug.Value,
            Summary = series.Description.Summary,
            Description = series.Description.Body,
            CategoryKey = series.CategoryKey,
            CategoryName = category?.Name,
            Facets = CommunityLifeSharedMapper.SplitFacets(category?.Facets),
            OrganizationId = series.OrganizationId?.Value,
            OrganizationName = organizationName,
            OrganizationSlug = organizationSlug,
            OrganizerUserId = series.OrganizerUserId.Value,
            HostName = series.HostName,
            IsOrganizerVerified = isOrganizerVerified,
            Recurrence = ToResponse(series.Recurrence, series.Schedule, series.Exceptions),
            StartDate = series.Schedule.FirstLocalDate,
            StartTime = series.Schedule.LocalStartTime,
            DurationMinutes = series.Schedule.DurationMinutes,
            TimeZoneId = series.Schedule.TimeZoneId,
            AttendanceMode = series.AttendanceMode.ToString(),
            Location = CommunityLifeSharedMapper.ToResponse(series.Location),
            Venue = new EventVenueResponse
            {
                Name = series.Venue.Name,
                AddressLine1 = series.Venue.AddressLine1,
                AddressLine2 = series.Venue.AddressLine2,
                Landmark = series.Venue.Landmark,
                PostalCode = series.Venue.PostalCode,
                Latitude = series.Venue.Latitude,
                Longitude = series.Venue.Longitude,
                OnlineUrl = series.Venue.OnlineUrl
            },
            Contact = CommunityLifeSharedMapper.ToResponse(series.Contact),
            Capacity = series.CapacityMaximum,
            RequiresRsvp = series.RequiresRsvp,
            IsAttendeeListPublic = series.IsAttendeeListPublic,
            IsFree = series.Audience.IsFree,
            IsFamilyFriendly = series.Audience.IsFamilyFriendly,
            IsChildFriendly = series.Audience.IsChildFriendly,
            IsStudentFocused = series.Audience.IsStudentFocused,
            DressCode = series.Audience.DressCode,
            AccessibilityNotes = series.Audience.AccessibilityNotes,
            Status = series.Status.ToString(),
            Visibility = series.Visibility.ToString(),
            NextOccurrence = nextOccurrence,
            OccurrenceCount = series.OccurrenceCount,
            CompletedCount = series.CompletedCount,
            CancelledOccurrenceCount = series.CancelledOccurrenceCount,
            MaterialisedThrough = series.MaterialisedThrough,
            CancellationReason = series.CancellationReason,
            Revision = series.Revision,
            PublishedAt = series.PublishedAt,
            CreatedAt = series.CreatedAt
        };
    }

    public static RecurrenceRuleResponse ToResponse(
        RecurrenceRule rule,
        SeriesSchedule schedule,
        IReadOnlyList<SeriesDateException>? exceptions = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(schedule);

        return new RecurrenceRuleResponse
        {
            Frequency = rule.Frequency.ToString(),
            Interval = rule.Interval,
            ByWeekday = [.. rule.Weekdays.Select(day => day == DayOfWeek.Sunday ? 7 : (int)day)],
            ByMonthDay = rule.MonthDays,
            EndKind = rule.EndKind.ToString(),
            Until = rule.Until,
            Count = rule.Count,
            Exceptions =
            [
                .. (exceptions ?? [])
                    .Select(exception => exception.LocalDate)
                    .OrderBy(date => date)
            ],
            Description = rule.Describe(schedule.FirstLocalDate)
        };
    }

    /// <summary>
    /// One date, as a card renders it.
    /// </summary>
    /// <param name="isNext">
    /// Whether this is the soonest date the series still has. Decided by the caller, which knows the
    /// whole page; a mapper looking at one row could not tell.
    /// </param>
    public static EventOccurrenceResponse ToOccurrence(
        CommunityEvent occurrence,
        SeriesSchedule schedule,
        bool viewerIsOrganizer = false,
        string? viewerRsvpStatus = null,
        bool isNext = false)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(schedule);

        return new EventOccurrenceResponse
        {
            OccurrenceId = occurrence.Id.Value,
            SeriesId = occurrence.SeriesId?.Value ?? Guid.Empty,
            Sequence = occurrence.SeriesSequence ?? 0,
            Title = occurrence.Title.Value,
            Slug = occurrence.Slug.Value,
            StartsAt = occurrence.Schedule.StartsAt,
            EndsAt = occurrence.Schedule.EndsAt,
            TimeZoneId = occurrence.Schedule.TimeZoneId,

            // The local date is computed from the occurrence's own start rather than read from the
            // rule, so a moved date reports where it actually is.
            LocalDate = schedule.ToLocalDate(occurrence.Schedule.StartsAt),
            Status = OccurrenceStatusProjection.For(occurrence).ToString(),
            EventStatus = occurrence.Status.ToString(),
            OriginalStartsAt = occurrence.OriginalStartsAt,
            CancellationReason = occurrence.CancellationReason,
            Location = CommunityLifeSharedMapper.ToResponse(occurrence.Location),
            VenueName = occurrence.Venue.Name,
            Capacity = CommunityEventMapper.ToResponse(occurrence, viewerIsOrganizer),
            ViewerRsvpStatus = viewerRsvpStatus,
            IsNext = isNext,
            Revision = occurrence.Revision
        };
    }
}
