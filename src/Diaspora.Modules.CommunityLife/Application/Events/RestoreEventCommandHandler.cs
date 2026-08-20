using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Brings an archived or deleted event back, as a draft.
/// <para>
/// <b>Loaded through <c>GetIncludingDeletedAsync</c></b>, because the global delete filter hides
/// exactly the row a restore exists to find. An ordinary read returns null and the handler answers
/// "not found" forever, which is a feature that fails silently in production rather than in a test.
/// </para>
/// <para>
/// <b>Never straight back to published, and the date is why.</b> An event restored a month after it
/// was taken down is an event whose time has usually passed; republishing it would put something
/// nobody can attend back in front of members. It returns as a draft, so the organizer reschedules
/// before anybody sees it.
/// </para>
/// </summary>
public sealed class RestoreEventCommandHandler
    : ICommandHandler<RestoreEventCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;
    private readonly IClock _clock;

    public RestoreEventCommandHandler(
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
        RestoreEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var communityEvent = await _events.GetIncludingDeletedAsync(
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

        if (communityEvent.IsDeleted)
        {
            communityEvent.Restore();
        }

        if (communityEvent.Status == EventStatus.Archived)
        {
            var restored = communityEvent.RestoreFromArchive(_clock.UtcNow, caller.Value);

            if (restored.IsFailure)
            {
                return Result.Failure<EventResponse>(restored.Error);
            }
        }

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }
}
