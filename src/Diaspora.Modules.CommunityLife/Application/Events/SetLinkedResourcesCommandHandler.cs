using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Replaces what an event points at elsewhere on the platform.
/// <para>
/// <b>The identifiers are deliberately not resolved.</b> Jobs and Marketplace publish no integration
/// directory, and building one so a career fair could name three vacancies would make two verticals
/// depend on each other to render an anchor. The client resolves each reference against the module
/// that owns it and drops what it cannot find, which is the same thing a stale link does anyway.
/// </para>
/// <para>
/// <b>What is validated is the shape.</b> An unknown resource type is refused rather than stored,
/// because a reference nothing can resolve is a dead link in an event page with an organizer's name
/// on it — and an external link with no address is dropped by the value object for the same reason.
/// </para>
/// </summary>
public sealed class SetLinkedResourcesCommandHandler
    : ICommandHandler<SetLinkedResourcesCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;

    public SetLinkedResourcesCommandHandler(
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
        SetLinkedResourcesCommand command, CancellationToken cancellationToken)
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

        var resources = new List<EventLinkedResource>(command.Request.Resources.Count);

        for (var index = 0; index < command.Request.Resources.Count; index++)
        {
            var resource = command.Request.Resources[index];

            if (CommunityLifeSharedMapper.ParseEnum<LinkedResourceType>(resource.ResourceType)
                is not { } type)
            {
                return Result.Failure<EventResponse>(
                    CommunityLifeErrors.LinkedResourceInvalid);
            }

            var built = EventLinkedResource.Create(
                type,
                resource.ResourceId ?? Guid.Empty,
                resource.Label,
                resource.Url,
                index);

            if (built is null)
            {
                return Result.Failure<EventResponse>(
                    CommunityLifeErrors.LinkedResourceInvalid);
            }

            resources.Add(built);
        }

        var applied = communityEvent.SetLinkedResources(resources);

        if (applied.IsFailure)
        {
            return Result.Failure<EventResponse>(applied.Error);
        }

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }
}
