using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Changes how many places an event has.
/// <para>
/// <b>Raising the ceiling does not promote the waiting list here.</b> Promotion happens on the path
/// that frees a place, inside the same unit of work, because a place that is free for the length of
/// a separate request is a place two people can be told they have. Raising capacity makes room; the
/// next withdrawal or the next sweep fills it.
/// </para>
/// </summary>
public sealed class SetEventCapacityCommandHandler
    : ICommandHandler<SetEventCapacityCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;

    public SetEventCapacityCommandHandler(
        ICommunityEventRepository events,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityEventReadContext read)
    {
        _events = events;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
    }

    public async Task<Result<EventResponse>> Handle(
        SetEventCapacityCommand command, CancellationToken cancellationToken)
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

        var applied = communityEvent.SetCapacity(
            command.Request.Capacity, command.Request.BaseRevision);

        if (applied.IsFailure)
        {
            return Result.Failure<EventResponse>(applied.Error);
        }

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }
}
