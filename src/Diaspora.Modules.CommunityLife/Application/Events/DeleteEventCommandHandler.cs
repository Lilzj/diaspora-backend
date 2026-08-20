using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Soft-deletes an event.
/// <para>
/// <b>Refused while people are still expecting to attend.</b> An event with live responses is
/// cancelled rather than deleted, so its attendees are told and their places are released — deleting
/// it would leave a dozen people planning to turn up somewhere that no longer exists, and nothing on
/// the platform would ever say otherwise.
/// </para>
/// <para>
/// Archived first, then deleted, in one transaction: the archival is what consumers de-index on, and
/// deleting alone would hide the row locally while every search index went on showing it.
/// </para>
/// </summary>
public sealed class DeleteEventCommandHandler : ICommandHandler<DeleteEventCommand>
{
    private readonly ICommunityEventRepository _events;
    private readonly IEventRsvpRepository _rsvps;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IClock _clock;

    public DeleteEventCommandHandler(
        ICommunityEventRepository events,
        IEventRsvpRepository rsvps,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IClock clock)
    {
        _events = events;
        _rsvps = rsvps;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result> Handle(
        DeleteEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(command.EventId), cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure(CommunityLifeErrors.EventNotFound);
        }

        var caller = await _authorization.RequireEventWriteAsync(
            communityEvent, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        if (communityEvent.Status is EventStatus.Published or EventStatus.InProgress)
        {
            var live = await _rsvps.FindLiveAsync(communityEvent.Id, 1, cancellationToken);

            if (live.Count > 0)
            {
                return Result.Failure(CommunityLifeErrors.EventHasAttendees);
            }
        }

        var now = _clock.UtcNow;

        var archived = communityEvent.Archive(now, caller.Value, reason: null);

        if (archived.IsFailure)
        {
            return archived;
        }

        communityEvent.MarkDeleted(now, caller.Value.Value.ToString());

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
