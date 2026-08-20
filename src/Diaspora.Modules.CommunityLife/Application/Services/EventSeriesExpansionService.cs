using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Writes a series' dates as real events.
/// <para>
/// <b>The single place an occurrence is ever created.</b> Creation from the series endpoint, top-up
/// from the sweep, and regeneration after a rule change all come through here, so there is exactly
/// one answer to what an occurrence is stamped with and exactly one place the horizon moves.
/// </para>
/// <para>
/// <b>Idempotent on the sequence number, and that is what makes it safe to retry.</b> Two sweeps
/// racing, a rebase that rewound the mark, a pass that failed after writing half a batch — all three
/// end up asking to write a sequence that already exists, and all three skip it. The check reads
/// deleted rows too, because a soft-deleted occurrence still holds its slug and regenerating the
/// date would collide with it.
/// </para>
/// <para>
/// <b>It does not save.</b> The caller owns the unit of work — a create commits the series and its
/// first dates together, and the sweep commits a batch — so this never decides when a transaction
/// ends.
/// </para>
/// </summary>
public sealed class EventSeriesExpansionService
{
    private readonly ICommunityEventRepository _events;
    private readonly IEventAnalyticsRepository _analytics;
    private readonly CommunityLifeSlugAllocator _slugs;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public EventSeriesExpansionService(
        ICommunityEventRepository events,
        IEventAnalyticsRepository analytics,
        CommunityLifeSlugAllocator slugs,
        IOptions<CommunityLifeOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _events = events;
        _analytics = analytics;
        _slugs = slugs;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    /// <summary>
    /// Writes whatever the series is missing up to the horizon, and records how far it got.
    /// </summary>
    /// <returns>The occurrences written, in sequence order. Empty when there was nothing to do.</returns>
    public async Task<IReadOnlyList<CommunityEvent>> ExpandAsync(
        EventSeries series, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (!series.GeneratesOccurrences)
        {
            return [];
        }

        var now = _clock.UtcNow;
        var options = _options.Value;

        var horizon = DateOnly.FromDateTime(
            now.UtcDateTime.AddDays(Math.Clamp(options.SeriesHorizonDays, 1, 3_650)));

        var written = await _events.FindOccurrenceSequencesAsync(series.Id, cancellationToken);
        var already = written.ToHashSet();

        var remaining = Math.Min(
            Math.Clamp(options.MaxOccurrencesPerExpansion, 1, CommunityLifeLimits.MaxSeriesOccurrences),
            CommunityLifeLimits.MaxSeriesOccurrences - already.Count);

        if (remaining <= 0)
        {
            return [];
        }

        // Expanded past the batch so a run whose first slots are all already written still finds
        // the unwritten ones behind them — which is exactly the shape a rewound mark produces.
        var slots = RecurrenceExpander.Expand(
            series.Recurrence,
            series.Schedule,
            series.ExceptionDates,
            fromSequence: series.LastMaterialisedSequence + 1,
            maxSlots: remaining + already.Count,
            notAfterLocalDate: horizon);

        var created = new List<CommunityEvent>();
        var lastSequence = series.LastMaterialisedSequence;
        DateOnly? lastDate = null;

        // The handles this run has issued. The repository can only see committed rows, so without
        // this every occurrence in one batch is offered the same free slug and the insert fails on
        // the unique index. Seeded with the bare title so every occurrence carries its own date —
        // "sunday-service-2026-08-09" is a link somebody can read, and one date silently holding
        // the undated handle would be the odd one out.
        var reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            CommunityLifeSlug.FromText(series.Title.Value).Value
        };

        foreach (var slot in slots)
        {
            lastSequence = Math.Max(lastSequence, slot.Sequence);
            lastDate = lastDate is { } current && current > slot.LocalDate ? current : slot.LocalDate;

            if (already.Contains(slot.Sequence))
            {
                continue;
            }

            if (created.Count >= remaining)
            {
                break;
            }

            var occurrence = await WriteAsync(series, slot, reserved, now, cancellationToken);

            if (occurrence is not null)
            {
                created.Add(occurrence);
            }
        }

        // The mark moves even when nothing was written. A series whose whole horizon is exceptions
        // would otherwise be re-examined by every sweep pass forever.
        if (lastDate is { } through)
        {
            series.RecordMaterialisation(lastSequence, through, created.Count, now);
        }
        else if (slots.Count == 0 && !HasDatesBeyondHorizon(series))
        {
            // The rule itself has run out — not merely the horizon. Nothing more will ever be
            // generated, so the series is finished rather than quiet, and saying so is what stops
            // the sweep reconsidering it on every pass for the rest of the deployment's life.
            series.Complete(now);
        }

        if (created.Count > 0)
        {
            CommunityLifeTelemetry.OccurrencesMaterialised.Add(created.Count);
        }

        return created;
    }

    /// <summary>
    /// Whether the rule still has dates past the point the horizon stopped at.
    /// <para>
    /// <b>The distinction between "finished" and "as far ahead as we write" — and getting it wrong
    /// completed every weekly series the moment it was published.</b> A horizon-bounded expansion
    /// that returns nothing means one of two entirely different things: the rule has run out, or the
    /// dates up to the horizon are already written. Only the first is completion. This asks the rule
    /// on its own, with no horizon, which terminates because the expander's walk is bounded.
    /// </para>
    /// </summary>
    private static bool HasDatesBeyondHorizon(EventSeries series) =>
        RecurrenceExpander.Expand(
            series.Recurrence,
            series.Schedule,
            series.ExceptionDates,
            fromSequence: series.LastMaterialisedSequence + 1,
            maxSlots: 1).Count > 0;

    /// <summary>
    /// Stamps one date out of the series template.
    /// <para>
    /// <b>Every failure here is skipped rather than propagated</b>, and the reason is the batch: one
    /// date whose slug allocation collides twelve times, or whose schedule is refused because the
    /// horizon reached a moment already past, must not abandon the twenty-five dates behind it. A
    /// skipped slot has no sequence written, so the next pass tries it again.
    /// </para>
    /// </summary>
    private async Task<CommunityEvent?> WriteAsync(
        EventSeries series,
        RecurrenceSlot slot,
        HashSet<string> reserved,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var schedule = EventSchedule.Create(
            slot.StartsAt,
            slot.EndsAt,
            series.Schedule.TimeZoneId,
            registrationDeadline: null,
            checkInOpensAt: null);

        if (schedule.IsFailure)
        {
            return null;
        }

        var slug = await _slugs.ForEventAsync(
            series.Title.Value, slot.StartsAt, reserved, cancellationToken);

        reserved.Add(slug.Value);

        // Fresh value objects, never the series' own instances. Handing an owned type to a second
        // aggregate re-parents it rather than copying it, and the first aggregate's columns are
        // written as nulls. See EventSeriesTemplate.
        var drafted = CommunityEvent.Draft(
            CommunityEventId.From(_guids.NewSequentialGuid()),
            series.OrganizationId,
            series.OrganizerUserId,
            EventSeriesTemplate.Title(series),
            slug,
            EventSeriesTemplate.Description(series),
            series.CategoryKey,
            schedule.Value,
            EventSeriesTemplate.Location(series),
            EventSeriesTemplate.Venue(series),
            series.AttendanceMode,
            now);

        if (drafted.IsFailure)
        {
            return null;
        }

        var occurrence = drafted.Value;

        var attached = occurrence.AttachToSeries(series.Id, slot.Sequence);

        if (attached.IsFailure)
        {
            return null;
        }

        var content = occurrence.UpdateContent(
            baseRevision: null,
            series.OrganizerUserId,
            audience: EventSeriesTemplate.Audience(series),
            contact: EventSeriesTemplate.Contact(series),
            hostName: series.HostName,
            requiresRsvp: series.RequiresRsvp,
            isAttendeeListPublic: series.IsAttendeeListPublic);

        if (content.IsFailure)
        {
            return null;
        }

        if (series.CapacityMaximum is { } maximum)
        {
            occurrence.SetCapacity(maximum, baseRevision: null);
        }

        occurrence.SetVisibility(series.Visibility, baseRevision: null);

        // A published series announces its dates as they are written. A date already past by the
        // time the sweep reached it is left as a draft rather than refused: it is a gap in a
        // calendar, not a failure, and publishing something that already finished would put a stale
        // date at the top of a discovery feed.
        if (series.PublishesOccurrences && !schedule.Value.HasEnded(now))
        {
            occurrence.Publish(now, series.OrganizerUserId);
        }

        await _events.AddAsync(occurrence, cancellationToken);

        await _analytics.AddAsync(
            EventAnalytics.Open(
                EventAnalyticsId.From(_guids.NewSequentialGuid()),
                occurrence.Id,
                series.OrganizationId,
                series.Location.CountryCode,
                series.CategoryKey,
                series.Location.NeighborhoodAreaId,
                now),
            cancellationToken);

        return occurrence;
    }
}
