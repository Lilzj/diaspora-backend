using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// Edits a repeating event at one of three scopes.
/// <para>
/// <b>The scope is the whole design, and it is the caller's to choose.</b> "Move the Tuesday meetup
/// to seven o'clock" means this week, or from now on, or the whole pattern — three acts with three
/// different sets of people to tell. This handler does exactly what the scope says and nothing more:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>ThisOccurrence</b> touches one event and marks it an exception, so later series-wide edits
/// step over it. The rule is untouched, and a request that tried to change the rule at this scope is
/// refused rather than quietly widened.
/// </item>
/// <item>
/// <b>ThisAndFuture</b> rebases the rule from the named occurrence's sequence and rewrites every
/// occurrence at or after it that has not already happened.
/// </item>
/// <item>
/// <b>EntireSeries</b> rewrites the template and every occurrence still ahead of the clock. Dates
/// already held are never touched — an event that happened cannot be moved, and rewriting its
/// attendance would erase who came.
/// </item>
/// </list>
/// <para>
/// <b>Individually edited occurrences are skipped at the two wide scopes.</b> The organizer who moved
/// one week to a different hall meant it, and a series-wide edit that quietly moved it back would
/// undo a deliberate act without saying so.
/// </para>
/// </summary>
public sealed class UpdateEventSeriesCommandHandler
    : ICommandHandler<UpdateEventSeriesCommand, EventSeriesResponse>
{
    private readonly IEventSeriesRepository _series;
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeNeighborhoodResolver _neighborhoods;
    private readonly EventSeriesExpansionService _expansion;
    private readonly EventSeriesReadContext _read;
    private readonly IClock _clock;

    public UpdateEventSeriesCommandHandler(
        IEventSeriesRepository series,
        ICommunityEventRepository events,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeNeighborhoodResolver neighborhoods,
        EventSeriesExpansionService expansion,
        EventSeriesReadContext read,
        IClock clock)
    {
        _series = series;
        _events = events;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _neighborhoods = neighborhoods;
        _expansion = expansion;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<EventSeriesResponse>> Handle(
        UpdateEventSeriesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var series = await _series.GetAsync(
            EventSeriesId.From(command.SeriesId), cancellationToken);

        if (series is null)
        {
            return Result.Failure<EventSeriesResponse>(CommunityLifeErrors.EventSeriesNotFound);
        }

        var caller = await _authorization.RequireSeriesWriteAsync(series, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(caller.Error);
        }

        var request = command.Request;

        if (CommunityLifeSharedMapper.ParseEnum<SeriesEditScope>(request.Scope)
            is not { } scope)
        {
            return Result.Failure<EventSeriesResponse>(CommunityLifeErrors.SeriesScopeInvalid);
        }

        var anchor = await ResolveAnchorAsync(series, scope, request, cancellationToken);

        if (anchor.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(anchor.Error);
        }

        var applied = scope == SeriesEditScope.ThisOccurrence
            ? await ApplyToOneAsync(series, anchor.Value!, request, caller.Value, cancellationToken)
            : await ApplyToSeriesAsync(series, scope, anchor.Value, request, caller.Value, cancellationToken);

        if (applied.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(series, cancellationToken));
    }

    /// <summary>
    /// The occurrence the scope is measured from.
    /// <para>
    /// Required for the two narrow scopes and refused for neither of them silently: guessing would
    /// move a hundred dates or none, which is exactly what the scope exists to prevent.
    /// </para>
    /// </summary>
    private async Task<Result<CommunityEvent?>> ResolveAnchorAsync(
        EventSeries series,
        SeriesEditScope scope,
        UpdateEventSeriesRequest request,
        CancellationToken cancellationToken)
    {
        if (scope == SeriesEditScope.EntireSeries)
        {
            return Result.Success<CommunityEvent?>(null);
        }

        if (request.OccurrenceId is not { } occurrenceId || occurrenceId == Guid.Empty)
        {
            return Result.Failure<CommunityEvent?>(CommunityLifeErrors.SeriesScopeInvalid);
        }

        var occurrence = await _events.GetAsync(
            CommunityEventId.From(occurrenceId), cancellationToken);

        if (occurrence is null || occurrence.SeriesId != series.Id)
        {
            return Result.Failure<CommunityEvent?>(CommunityLifeErrors.OccurrenceNotFound);
        }

        return Result.Success<CommunityEvent?>(occurrence);
    }

    /// <summary>
    /// One date, edited on its own.
    /// <para>
    /// Delegates to the ordinary event methods — the same content update, the same relocation, the
    /// same occurrence reschedule any single date gets — because an occurrence is an event and
    /// editing one through a second code path would be two implementations of one act.
    /// </para>
    /// </summary>
    private async Task<Result> ApplyToOneAsync(
        EventSeries series,
        CommunityEvent occurrence,
        UpdateEventSeriesRequest request,
        UserId caller,
        CancellationToken cancellationToken)
    {
        if (request.Recurrence is not null)
        {
            return Result.Failure(CommunityLifeErrors.SeriesScopeInvalid);
        }

        var changed = new List<string>();

        var content = BuildContent(request);

        if (content.IsFailure)
        {
            return Result.Failure(content.Error);
        }

        var updated = occurrence.UpdateContent(
            request.BaseRevision,
            caller,
            content.Value.Title,
            content.Value.Description,
            audience: null,
            content.Value.Contact,
            request.HostName,
            request.RequiresRsvp,
            request.IsAttendeeListPublic);

        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error);
        }

        changed.AddRange(updated.Value);

        if (request.Capacity is { } capacity)
        {
            var set = occurrence.SetCapacity(capacity, baseRevision: null);

            if (set.IsFailure)
            {
                return set;
            }

            changed.Add(nameof(CommunityEvent.Capacity));
        }

        var relocated = await RelocateAsync(occurrence, request, caller, cancellationToken);

        if (relocated.IsFailure)
        {
            return relocated;
        }

        changed.AddRange(relocated.Value);

        var moved = MoveOccurrence(series, occurrence, request, caller);

        if (moved.IsFailure)
        {
            return Result.Failure(moved.Error);
        }

        changed.AddRange(moved.Value);

        if (changed.Count == 0)
        {
            return Result.Success();
        }

        // Marked whatever changed, not only a move: an occurrence whose title or hall the organizer
        // set by hand is one a series-wide edit must leave alone.
        occurrence.MarkSeriesException();
        _events.Update(occurrence);

        series.RaiseUpdated(
            SeriesEditScope.ThisOccurrence,
            changed,
            caller,
            isMaterialChange: moved.Value.Count > 0 || relocated.Value.Count > 0,
            affectedOccurrenceCount: 1);

        _series.Update(series);

        return Result.Success();
    }

    /// <summary>
    /// The template and every occurrence within scope.
    /// <para>
    /// The rule is rebased first, then the occurrences are rewritten from it, then the expansion
    /// service fills whatever the new rule reaches that the old one did not. The order matters: an
    /// expansion run before the rebase would write dates from the rule being replaced.
    /// </para>
    /// </summary>
    private async Task<Result> ApplyToSeriesAsync(
        EventSeries series,
        SeriesEditScope scope,
        CommunityEvent? anchor,
        UpdateEventSeriesRequest request,
        UserId caller,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var changed = new List<string>();

        var content = BuildContent(request);

        if (content.IsFailure)
        {
            return Result.Failure(content.Error);
        }

        var updated = series.UpdateContent(
            request.BaseRevision,
            content.Value.Title,
            content.Value.Description,
            audience: null,
            content.Value.Contact,
            request.HostName,
            request.Capacity,
            request.RequiresRsvp,
            request.IsAttendeeListPublic);

        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error);
        }

        changed.AddRange(updated.Value);

        var relocated = await RelocateSeriesAsync(series, request, cancellationToken);

        if (relocated.IsFailure)
        {
            return Result.Failure(relocated.Error);
        }

        changed.AddRange(relocated.Value);

        var fromSequence = scope == SeriesEditScope.ThisAndFuture
            ? anchor?.SeriesSequence ?? 1
            : 1;

        var rebased = Rebase(series, request, fromSequence);

        if (rebased.IsFailure)
        {
            return Result.Failure(rebased.Error);
        }

        changed.AddRange(rebased.Value);

        if (changed.Count == 0)
        {
            return Result.Success();
        }

        var affected = await RewriteOccurrencesAsync(
            series, fromSequence, now, caller, cancellationToken);

        _series.Update(series);

        // Fills whatever the new rule reaches that the old one did not. A weekly rule widened to
        // twice weekly has twice as many dates inside the same horizon, and without this they would
        // not appear until the sweep next ran.
        await _expansion.ExpandAsync(series, cancellationToken);

        series.RaiseUpdated(
            scope,
            changed,
            caller,
            isMaterialChange: rebased.Value.Count > 0 || relocated.Value.Count > 0,
            affected);

        return Result.Success();
    }

    /// <summary>
    /// Moves the occurrences the rule now says something different about.
    /// <para>
    /// <b>Only what is still ahead, and only what the rule still owns.</b> Both conditions are in the
    /// query rather than here, so an occurrence that already happened is never loaded and an
    /// individually edited one is never considered.
    /// </para>
    /// </summary>
    private async Task<int> RewriteOccurrencesAsync(
        EventSeries series,
        int fromSequence,
        DateTimeOffset now,
        UserId caller,
        CancellationToken cancellationToken)
    {
        var occurrences = await _events.FindOccurrencesToRewriteAsync(
            series.Id,
            fromSequence,
            now,
            includeExceptions: false,
            CommunityLifeLimits.MaxSeriesOccurrences,
            cancellationToken);

        if (occurrences.Count == 0)
        {
            return 0;
        }

        var slots = RecurrenceExpander
            .Expand(
                series.Recurrence,
                series.Schedule,
                series.ExceptionDates,
                fromSequence,
                CommunityLifeLimits.MaxSeriesOccurrences)
            .ToDictionary(slot => slot.Sequence);

        var affected = 0;

        foreach (var occurrence in occurrences)
        {
            var rewritten = Rewrite(series, occurrence, slots, caller, now);

            if (rewritten)
            {
                affected++;
            }
        }

        return affected;
    }

    /// <summary>
    /// One occurrence, restated from the template and the rule.
    /// <para>
    /// A date the rule no longer generates is cancelled rather than left standing: a weekly meeting
    /// changed to monthly must not leave three orphaned Tuesdays on the calendar that belong to
    /// nothing and that the series will never mention again.
    /// </para>
    /// </summary>
    private bool Rewrite(
        EventSeries series,
        CommunityEvent occurrence,
        Dictionary<int, RecurrenceSlot> slots,
        UserId caller,
        DateTimeOffset now)
    {
        if (occurrence.SeriesSequence is not { } sequence
            || !slots.TryGetValue(sequence, out var slot))
        {
            occurrence.Cancel(now, caller, "The repeating event's pattern changed.");
            _events.Update(occurrence);

            return true;
        }

        var changed = false;

        var schedule = EventSchedule.Create(
            slot.StartsAt,
            slot.EndsAt,
            series.Schedule.TimeZoneId,
            registrationDeadline: null,
            checkInOpensAt: null);

        if (schedule.IsSuccess && schedule.Value != occurrence.Schedule)
        {
            // The ordinary reschedule, not the occurrence one: this date is moving because the rule
            // moved, so it is still where the rule puts it and is not an exception to anything.
            changed |= occurrence.Reschedule(schedule.Value, null, caller, now).IsSuccess;
        }

        // Fresh value objects, never the series' own instances — handing an owned type to a second
        // aggregate re-parents it rather than copying it. See EventSeriesTemplate.
        var content = occurrence.UpdateContent(
            baseRevision: null,
            caller,
            EventSeriesTemplate.Title(series),
            EventSeriesTemplate.Description(series),
            EventSeriesTemplate.Audience(series),
            EventSeriesTemplate.Contact(series),
            series.HostName,
            series.RequiresRsvp,
            series.IsAttendeeListPublic);

        changed |= content.IsSuccess && content.Value.Count > 0;

        if (occurrence.Location != series.Location
            || occurrence.Venue != series.Venue
            || occurrence.AttendanceMode != series.AttendanceMode)
        {
            changed |= occurrence
                .Relocate(
                    EventSeriesTemplate.Location(series),
                    EventSeriesTemplate.Venue(series),
                    series.AttendanceMode,
                    null,
                    caller)
                .IsSuccess;
        }

        if (series.CapacityMaximum != occurrence.Capacity.Maximum)
        {
            // Refused below what is already committed, and that refusal is correct even here: a
            // series shrunk to forty seats cannot un-seat the sixty people already told they had one.
            changed |= occurrence.SetCapacity(series.CapacityMaximum, null).IsSuccess;
        }

        if (changed)
        {
            _events.Update(occurrence);
        }

        return changed;
    }

    /// <summary>
    /// Restates the rule and the local time, and rewinds the horizon to where the edit takes effect.
    /// <para>
    /// Synchronous: nothing here reads anything. The rewind is what makes the expansion service
    /// rewrite from the scope's first occurrence rather than leave the old rule's dates standing.
    /// </para>
    /// </summary>
    private static Result<IReadOnlyList<string>> Rebase(
        EventSeries series,
        UpdateEventSeriesRequest request,
        int fromSequence)
    {
        var recurrence = request.Recurrence is null
            ? null
            : RecurrenceRuleFactory.Create(request.Recurrence);

        if (recurrence is { IsFailure: true })
        {
            return Result.Failure<IReadOnlyList<string>>(recurrence.Error);
        }

        var wantsSchedule = request.StartTime is not null
            || request.DurationMinutes is not null
            || request.TimeZoneId is not null;

        SeriesSchedule? schedule = null;

        if (wantsSchedule)
        {
            var built = SeriesSchedule.Create(
                series.Schedule.FirstLocalDate,
                request.StartTime ?? series.Schedule.LocalStartTime,
                request.DurationMinutes ?? series.Schedule.DurationMinutes,
                request.TimeZoneId ?? series.Schedule.TimeZoneId);

            if (built.IsFailure)
            {
                return Result.Failure<IReadOnlyList<string>>(built.Error);
            }

            schedule = built.Value;
        }

        return series.Rebase(
            recurrence?.Value, schedule, fromSequence, baseRevision: null);
    }

    private async Task<Result<IReadOnlyList<string>>> RelocateSeriesAsync(
        EventSeries series,
        UpdateEventSeriesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CountryCode is null && request.Venue is null && request.AttendanceMode is null)
        {
            return Result.Success<IReadOnlyList<string>>([]);
        }

        var location = await ResolveLocationAsync(
            series.Location, request, cancellationToken);

        if (location.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(location.Error);
        }

        var venue = request.Venue is null
            ? Result.Success(series.Venue)
            : EventVenue.Create(
                request.Venue.Name,
                request.Venue.AddressLine1,
                request.Venue.AddressLine2,
                request.Venue.Landmark,
                request.Venue.PostalCode,
                request.Venue.Latitude,
                request.Venue.Longitude,
                request.Venue.OnlineUrl);

        if (venue.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(venue.Error);
        }

        var mode = CommunityLifeSharedMapper.ParseEnum<EventAttendanceMode>(request.AttendanceMode)
            ?? series.AttendanceMode;

        return series.Relocate(location.Value, venue.Value, mode, baseRevision: null);
    }

    private async Task<Result<IReadOnlyList<string>>> RelocateAsync(
        CommunityEvent occurrence,
        UpdateEventSeriesRequest request,
        UserId caller,
        CancellationToken cancellationToken)
    {
        if (request.CountryCode is null && request.Venue is null && request.AttendanceMode is null)
        {
            return Result.Success<IReadOnlyList<string>>([]);
        }

        var location = await ResolveLocationAsync(
            occurrence.Location, request, cancellationToken);

        if (location.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(location.Error);
        }

        var venue = request.Venue is null
            ? Result.Success(occurrence.Venue)
            : EventVenue.Create(
                request.Venue.Name,
                request.Venue.AddressLine1,
                request.Venue.AddressLine2,
                request.Venue.Landmark,
                request.Venue.PostalCode,
                request.Venue.Latitude,
                request.Venue.Longitude,
                request.Venue.OnlineUrl);

        if (venue.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(venue.Error);
        }

        var mode = CommunityLifeSharedMapper.ParseEnum<EventAttendanceMode>(request.AttendanceMode)
            ?? occurrence.AttendanceMode;

        var relocated = occurrence.Relocate(location.Value, venue.Value, mode, null, caller);

        return relocated.IsFailure
            ? Result.Failure<IReadOnlyList<string>>(relocated.Error)
            : Result.Success<IReadOnlyList<string>>([nameof(CommunityEvent.Location)]);
    }

    private async Task<Result<CommunityLifeLocationContext>> ResolveLocationAsync(
        CommunityLifeLocationContext current,
        UpdateEventSeriesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CountryCode is null)
        {
            return Result.Success(current.WithPlaceNames(
                request.RegionName ?? current.RegionName,
                request.CityName ?? current.CityName));
        }

        var context = CommunityLifeLocationContext.Create(
            request.CountryCode,
            request.RegionName ?? current.RegionName,
            request.CityName ?? current.CityName);

        return context.IsFailure
            ? context
            : await _neighborhoods.ResolveAsync(
                context.Value, request.NeighborhoodAreaId, cancellationToken);
    }

    /// <summary>Moves one date, when the request named a new one. See the class summary.</summary>
    private Result<IReadOnlyList<string>> MoveOccurrence(
        EventSeries series,
        CommunityEvent occurrence,
        UpdateEventSeriesRequest request,
        UserId caller)
    {
        if (request.StartTime is null && request.DurationMinutes is null)
        {
            return Result.Success<IReadOnlyList<string>>([]);
        }

        var now = _clock.UtcNow;
        var localDate = series.Schedule.ToLocalDate(occurrence.Schedule.StartsAt);

        var moved = SeriesSchedule.Create(
            localDate,
            request.StartTime ?? series.Schedule.LocalStartTime,
            request.DurationMinutes
                ?? (int)(occurrence.Schedule.EndsAt - occurrence.Schedule.StartsAt).TotalMinutes,
            request.TimeZoneId ?? series.Schedule.TimeZoneId);

        if (moved.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(moved.Error);
        }

        var schedule = EventSchedule.Create(
            moved.Value.ToStart(localDate),
            moved.Value.ToEnd(localDate),
            moved.Value.TimeZoneId,
            registrationDeadline: null,
            checkInOpensAt: null);

        if (schedule.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(schedule.Error);
        }

        var rescheduled = occurrence.RescheduleOccurrence(
            schedule.Value, request.BaseRevision, caller, now);

        if (rescheduled.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(rescheduled.Error);
        }

        // The generated date leaves the rule, so the next expansion does not put it back.
        var excluded = series.AddException(localDate, SeriesExceptionReason.Moved, now);

        return excluded.IsFailure
            ? Result.Failure<IReadOnlyList<string>>(excluded.Error)
            : Result.Success<IReadOnlyList<string>>([nameof(CommunityEvent.Schedule)]);
    }

    private static Result<TemplateContent> BuildContent(UpdateEventSeriesRequest request)
    {
        CommunityLifeTitle? title = null;

        if (request.Title is not null)
        {
            var built = CommunityLifeTitle.Create(request.Title);

            if (built.IsFailure)
            {
                return Result.Failure<TemplateContent>(built.Error);
            }

            title = built.Value;
        }

        CommunityLifeDescription? description = null;

        if (request.Summary is not null || request.Description is not null)
        {
            var built = CommunityLifeDescription.Create(request.Summary, request.Description);

            if (built.IsFailure)
            {
                return Result.Failure<TemplateContent>(built.Error);
            }

            description = built.Value;
        }

        ContactDetails? contact = null;

        if (request.Contact is not null)
        {
            var built = ContactDetails.Create(
                request.Contact.Phone,
                request.Contact.WhatsApp,
                request.Contact.Email,
                request.Contact.Website,
                request.Contact.ContactName);

            if (built.IsFailure)
            {
                return Result.Failure<TemplateContent>(built.Error);
            }

            contact = built.Value;
        }

        return Result.Success(new TemplateContent(title, description, contact));
    }

    /// <summary>
    /// The three template fields a series edit can carry. Deliberately no audience: the update
    /// request has no family-friendly, student or accessibility fields, so there is nothing to
    /// build one from — and a nullable member that was always null would invite somebody to wire it
    /// to a request field that does not exist.
    /// </summary>
    private sealed record TemplateContent(
        CommunityLifeTitle? Title,
        CommunityLifeDescription? Description,
        ContactDetails? Contact);
}
