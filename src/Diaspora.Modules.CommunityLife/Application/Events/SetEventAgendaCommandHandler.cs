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
/// Replaces an event's running order.
/// <para>
/// Offsets from the start rather than wall-clock times, so an agenda survives a reschedule. Moving a
/// conference by a week should not mean retyping fourteen session times.
/// </para>
/// </summary>
public sealed class SetEventAgendaCommandHandler
    : ICommandHandler<SetEventAgendaCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;
    private readonly IGuidGenerator _guids;

    public SetEventAgendaCommandHandler(
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
        SetEventAgendaCommand command, CancellationToken cancellationToken)
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

        var items = command.Request.Items
            .Select((item, index) => EventAgendaItem.Create(
                _guids.NewSequentialGuid(),
                item.Title,
                item.Description,
                item.StartOffsetMinutes,
                item.DurationMinutes,
                item.SpeakerName,
                index))
            .ToList();

        var applied = communityEvent.SetAgenda(items, command.Request.BaseRevision);

        if (applied.IsFailure)
        {
            return Result.Failure<EventResponse>(applied.Error);
        }

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }
}
