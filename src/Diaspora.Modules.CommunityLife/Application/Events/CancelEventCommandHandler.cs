using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Calls an event off and releases every place.
/// <para>
/// <b>Every response is released with its own decline reason, and that reason is the point.</b>
/// Somebody whose event was cancelled did nothing wrong; a reputation signal that could not tell
/// their released place from a no-show would punish them for the organizer's decision, and the
/// signals downstream are computed from exactly this field.
/// </para>
/// <para>
/// <b>Released in batches inside one transaction.</b> A festival with two thousand responses is
/// released in pages rather than loaded whole, and the whole release commits with the cancellation —
/// an event marked cancelled while half its guest list still held places would tell those people
/// they were expected somewhere that is not happening.
/// </para>
/// </summary>
public sealed class CancelEventCommandHandler
    : ICommandHandler<CancelEventCommand, EventResponse>
{
    private const int BatchSize = 200;

    private readonly ICommunityEventRepository _events;
    private readonly IEventRsvpRepository _rsvps;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CancelEventCommandHandler(
        ICommunityEventRepository events,
        IEventRsvpRepository rsvps,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityEventReadContext read,
        IGuidGenerator guids,
        IClock clock)
    {
        _events = events;
        _rsvps = rsvps;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<EventResponse>> Handle(
        CancelEventCommand command, CancellationToken cancellationToken)
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

        // Cancelled first, so the domain event carries the number of people it affected rather
        // than a count the release has already driven to zero.
        var cancelled = communityEvent.Cancel(now, caller.Value, command.Request.Reason);

        if (cancelled.IsFailure)
        {
            return Result.Failure<EventResponse>(cancelled.Error);
        }

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await ReleaseResponsesAsync(communityEvent.Id, now, cancellationToken);

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }

    /// <summary>
    /// Releases every live response, in pages.
    /// <para>
    /// The counters settle through the ordinary status handler, so the event's attendance figures
    /// fall to zero by the same path every other release uses rather than being zeroed by hand.
    /// </para>
    /// </summary>
    private async Task ReleaseResponsesAsync(
        CommunityEventId eventId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        while (true)
        {
            var batch = await _rsvps.FindLiveAsync(eventId, BatchSize, cancellationToken);

            if (batch.Count == 0)
            {
                return;
            }

            foreach (var rsvp in batch)
            {
                var moved = rsvp.ReleaseForCancelledEvent(_guids.NewSequentialGuid(), now);

                if (moved.IsSuccess)
                {
                    _rsvps.Update(rsvp);
                }
            }

            // Saved before the next page is read. The query selects live responses, so without
            // committing the batch the next read would return the same rows and this would not
            // terminate.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (batch.Count < BatchSize)
            {
                return;
            }
        }
    }
}
