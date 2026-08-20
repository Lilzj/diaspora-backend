using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// Calls a repeating event off, or pauses it.
/// <para>
/// <b>Pausing and cancelling are one endpoint and two entirely different acts.</b> Pausing stops the
/// horizon being topped up and tells nobody anything, because nothing announced has changed — a
/// committee breaking for the summer has not cancelled September. Cancelling calls off every future
/// date, which releases every place and raises a cancellation per occurrence, because each has its
/// own guest list and each of those people has to be told.
/// </para>
/// <para>
/// <b>Dates already held are never touched by either.</b> An event that happened cannot be
/// un-happened, and rewriting its attendance would erase the record of who came.
/// </para>
/// </summary>
public sealed class CancelEventSeriesCommandHandler
    : ICommandHandler<CancelEventSeriesCommand, EventSeriesResponse>
{
    private readonly IEventSeriesRepository _series;
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly EventSeriesReadContext _read;
    private readonly IClock _clock;

    public CancelEventSeriesCommandHandler(
        IEventSeriesRepository series,
        ICommunityEventRepository events,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        EventSeriesReadContext read,
        IClock clock)
    {
        _series = series;
        _events = events;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<EventSeriesResponse>> Handle(
        CancelEventSeriesCommand command, CancellationToken cancellationToken)
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

        var now = _clock.UtcNow;

        if (command.Request.PauseOnly)
        {
            var paused = series.Pause(now, caller.Value, command.Request.Reason);

            if (paused.IsFailure)
            {
                return Result.Failure<EventSeriesResponse>(paused.Error);
            }

            _series.Update(series);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(await _read.ToResponseAsync(series, cancellationToken));
        }

        var cancelled = series.Cancel(now, caller.Value, command.Request.Reason);

        if (cancelled.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(cancelled.Error);
        }

        _series.Update(series);

        var occurrences = await _events.FindOccurrencesToRewriteAsync(
            series.Id,
            fromSequence: 1,
            notBefore: now,

            // Individually moved dates go too. Somebody who shifted one week's meeting to the
            // Wednesday is still coming to a meeting the organizer has just called off.
            includeExceptions: true,
            CommunityLifeLimits.MaxSeriesOccurrences,
            cancellationToken);

        var affected = 0;

        foreach (var occurrence in occurrences)
        {
            if (occurrence.Cancel(now, caller.Value, command.Request.Reason).IsFailure)
            {
                continue;
            }

            _events.Update(occurrence);
            series.RecordOccurrenceCancelled(now);
            affected++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (affected > 0)
        {
            CommunityLifeTelemetry.EventsCancelled.Add(
                affected, new KeyValuePair<string, object?>("source", "series"));
        }

        return Result.Success(await _read.ToResponseAsync(series, cancellationToken));
    }
}
