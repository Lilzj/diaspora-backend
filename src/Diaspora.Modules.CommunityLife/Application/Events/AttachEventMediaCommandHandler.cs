using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
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
/// Attaches an uploaded asset to an event.
/// <para>
/// The claim goes to Media after the save, so an unclaimed asset is the worst outcome of a Media
/// outage rather than an organizer's work refused. Verification evidence is not attachable here: it
/// belongs to organizations and reaches the aggregate only through a verification submission.
/// </para>
/// </summary>
public sealed class AttachEventMediaCommandHandler
    : ICommandHandler<AttachEventMediaCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeMediaClaimer _media;
    private readonly CommunityEventReadContext _read;
    private readonly IClock _clock;

    public AttachEventMediaCommandHandler(
        ICommunityEventRepository events,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeMediaClaimer media,
        CommunityEventReadContext read,
        IClock clock)
    {
        _events = events;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _media = media;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<EventResponse>> Handle(
        AttachEventMediaCommand command, CancellationToken cancellationToken)
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

        var role = CommunityLifeSharedMapper.ParseEnum<CommunityLifeMediaRole>(
            command.Request.Role) ?? CommunityLifeMediaRole.Gallery;

        if (role is CommunityLifeMediaRole.VerificationEvidence or CommunityLifeMediaRole.Logo)
        {
            return Result.Failure<EventResponse>(CommunityLifeErrors.MediaInvalid);
        }

        var attached = communityEvent.AttachMedia(
            command.Request.MediaId, role, command.Request.Caption, _clock.UtcNow);

        if (attached.IsFailure)
        {
            return Result.Failure<EventResponse>(attached.Error);
        }

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _media.ClaimForEventAsync(
            communityEvent.Id.Value,
            command.Request.MediaId,
            role.ToString(),
            cancellationToken: cancellationToken);

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }
}
