using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Moves an event in time.
/// <para>
/// <b>The clock is passed to the schedule so a move into the past is refused.</b> An event
/// rescheduled to last Tuesday would sit in every attendee's calendar as something they had already
/// missed.
/// </para>
/// <para>
/// <b>The responses are deliberately left alone.</b> A body that moved its meeting by an hour has
/// not lost its guest list, and releasing two hundred places to make people re-confirm would cost
/// the organizer most of them. Attendees are told, through the material-change flag on the event
/// this raises, and decide for themselves.
/// </para>
/// </summary>
public sealed class RescheduleEventCommandHandler
    : ICommandHandler<RescheduleEventCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;
    private readonly IClock _clock;

    public RescheduleEventCommandHandler(
        ICommunityEventRepository events,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityEventReadContext read,
        IClock clock)
    {
        _events = events;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<EventResponse>> Handle(
        RescheduleEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(command.EventId), cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure<EventResponse>(CommunityLifeErrors.EventNotFound);
        }

        var caller = await _authorization.RequireEventWriteAsync(
            communityEvent, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<EventResponse>(caller.Error);
        }

        var now = _clock.UtcNow;
        var request = command.Request;

        var schedule = EventSchedule.Create(
            request.StartsAt,
            request.EndsAt,
            request.TimeZoneId ?? communityEvent.Schedule.TimeZoneId,
            request.RegistrationDeadline,
            request.CheckInOpensAt,
            now);

        if (schedule.IsFailure)
        {
            return Result.Failure<EventResponse>(schedule.Error);
        }

        var moved = communityEvent.Reschedule(
            schedule.Value, request.BaseRevision, caller.Value, now);

        if (moved.IsFailure)
        {
            return Result.Failure<EventResponse>(moved.Error);
        }

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }
}
