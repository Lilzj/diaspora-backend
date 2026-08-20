using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Replaces an event's speaker list.
/// <para>
/// A speaker is a name on a page rather than an account. Most people who speak at a diaspora event
/// have never signed in, and requiring a member identifier would leave half the programme unnameable.
/// </para>
/// </summary>
public sealed class SetEventSpeakersCommandHandler
    : ICommandHandler<SetEventSpeakersCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;
    private readonly IGuidGenerator _guids;

    public SetEventSpeakersCommandHandler(
        ICommunityEventRepository events,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityEventReadContext read,
        IGuidGenerator guids)
    {
        _events = events;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _guids = guids;
    }

    public async Task<Result<EventResponse>> Handle(
        SetEventSpeakersCommand command, CancellationToken cancellationToken)
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

        var speakers = command.Request.Speakers
            .Select((speaker, index) => EventSpeaker.Create(
                _guids.NewSequentialGuid(),
                speaker.Name,
                speaker.Title,
                speaker.Organization,
                speaker.Biography,
                speaker.PhotoMediaId,
                speaker.MemberUserId,
                index))
            .ToList();

        var applied = communityEvent.SetSpeakers(speakers, command.Request.BaseRevision);

        if (applied.IsFailure)
        {
            return Result.Failure<EventResponse>(applied.Error);
        }

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }
}
