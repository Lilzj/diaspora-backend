using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// Moves or calls off one date of a repeating event.
/// <para>
/// <b>Both acts write an exception on the rule, and that is the half a naive implementation
/// forgets.</b> Cancelling one week without one leaves the rule still generating that date, so the
/// next expansion writes it again as a fresh occurrence with a fresh identifier — and everybody who
/// was told the meeting was off is silently re-invited to it.
/// </para>
/// <para>
/// <b>There is deliberately no way back from cancelled to scheduled.</b> Cancelling releases every
/// place and tells every attendee; un-cancelling would put the date back on their calendar without
/// putting their place back. Running it again is a new date, which is what the organizer means.
/// </para>
/// </summary>
public sealed class UpdateOccurrenceCommandHandler
    : ICommandHandler<UpdateOccurrenceCommand, EventOccurrenceResponse>
{
    private const string CancelledStatus = "Cancelled";

    private readonly ICommunityEventRepository _events;
    private readonly IEventSeriesRepository _series;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IClock _clock;

    public UpdateOccurrenceCommandHandler(
        ICommunityEventRepository events,
        IEventSeriesRepository series,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IClock clock)
    {
        _events = events;
        _series = series;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<EventOccurrenceResponse>> Handle(
        UpdateOccurrenceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var occurrence = await _events.GetAsync(
            CommunityEventId.From(command.OccurrenceId), cancellationToken);

        if (occurrence?.SeriesId is not { } seriesId)
        {
            return Result.Failure<EventOccurrenceResponse>(
                CommunityLifeErrors.OccurrenceNotFound);
        }

        var series = await _series.GetAsync(seriesId, cancellationToken);

        if (series is null)
        {
            return Result.Failure<EventOccurrenceResponse>(
                CommunityLifeErrors.EventSeriesNotFound);
        }

        var caller = await _authorization.RequireEventWriteAsync(occurrence, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<EventOccurrenceResponse>(caller.Error);
        }

        var now = _clock.UtcNow;

        // The generated date, before anything moves. Read from the occurrence's original start when
        // it has already been moved once, because that — not where it currently sits — is the date
        // the rule holds.
        var generatedDate = series.Schedule.ToLocalDate(
            occurrence.OriginalStartsAt ?? occurrence.Schedule.StartsAt);

        var applied = string.Equals(
            command.Request.Status?.Trim(), CancelledStatus, StringComparison.OrdinalIgnoreCase)
            ? CancelOne(series, occurrence, command, generatedDate, caller.Value, now)
            : MoveOne(series, occurrence, command, generatedDate, caller.Value, now);

        if (applied.IsFailure)
        {
            return Result.Failure<EventOccurrenceResponse>(applied.Error);
        }

        _events.Update(occurrence);
        _series.Update(series);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            EventSeriesMapper.ToOccurrence(
                occurrence, series.Schedule, viewerIsOrganizer: true));
    }

    private static Result CancelOne(
        EventSeries series,
        CommunityEvent occurrence,
        UpdateOccurrenceCommand command,
        DateOnly generatedDate,
        SharedKernel.Identity.UserId caller,
        DateTimeOffset now)
    {
        var revision = occurrence.EnsureRevision(command.Request.BaseRevision);

        if (revision.IsFailure)
        {
            return revision;
        }

        var cancelled = occurrence.Cancel(now, caller, command.Request.Reason);

        if (cancelled.IsFailure)
        {
            return cancelled;
        }

        occurrence.MarkSeriesException();

        var excluded = series.AddException(
            generatedDate, SeriesExceptionReason.Cancelled, now);

        if (excluded.IsFailure)
        {
            return excluded;
        }

        series.RecordOccurrenceCancelled(now);

        return Result.Success();
    }

    private static Result MoveOne(
        EventSeries series,
        CommunityEvent occurrence,
        UpdateOccurrenceCommand command,
        DateOnly generatedDate,
        SharedKernel.Identity.UserId caller,
        DateTimeOffset now)
    {
        var request = command.Request;

        if (request.Date is null && request.StartTime is null && request.DurationMinutes is null)
        {
            return Result.Failure(CommunityLifeErrors.ScheduleInvalid);
        }

        var localDate = request.Date ?? series.Schedule.ToLocalDate(occurrence.Schedule.StartsAt);

        var moved = SeriesSchedule.Create(
            localDate,
            request.StartTime ?? series.Schedule.LocalStartTime,
            request.DurationMinutes
                ?? (int)(occurrence.Schedule.EndsAt - occurrence.Schedule.StartsAt).TotalMinutes,
            series.Schedule.TimeZoneId);

        if (moved.IsFailure)
        {
            return Result.Failure(moved.Error);
        }

        var schedule = EventSchedule.Create(
            moved.Value.ToStart(localDate),
            moved.Value.ToEnd(localDate),
            series.Schedule.TimeZoneId,
            registrationDeadline: null,
            checkInOpensAt: null);

        if (schedule.IsFailure)
        {
            return Result.Failure(schedule.Error);
        }

        var rescheduled = occurrence.RescheduleOccurrence(
            schedule.Value, request.BaseRevision, caller, now);

        return rescheduled.IsFailure
            ? rescheduled
            : series.AddException(generatedDate, SeriesExceptionReason.Moved, now);
    }
}
