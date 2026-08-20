using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Moderation;

/// <summary>
/// Hides, suspends or clears an event or an organization.
/// <para>
/// <b>Loaded past the delete filter.</b> A moderator reviewing something that has already been taken
/// down needs to reach it; an ordinary read would answer not-found for exactly the rows the queue
/// points at.
/// </para>
/// <para>
/// <b>Hiding and suspending are different acts.</b> A hidden page has a problem on it and its owner
/// can still fix it. A suspended body is one the platform has stopped vouching for, and its events
/// stop appearing too — which is why the aggregate moves the lifecycle as well.
/// </para>
/// </summary>
public sealed class ModerateSubjectCommandHandler : ICommandHandler<ModerateSubjectCommand>
{
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IClock _clock;

    public ModerateSubjectCommandHandler(
        ICommunityEventRepository events,
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IClock clock)
    {
        _events = events;
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result> Handle(
        ModerateSubjectCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure(staff.Error);
        }

        if (CommunityLifeSharedMapper.ParseEnum<ReportSubjectType>(command.SubjectType)
            is not { } subjectType)
        {
            return Result.Failure(CommunityLifeErrors.ReportSubjectInvalid);
        }

        if (CommunityLifeSharedMapper.ParseEnum<CommunityLifeModerationState>(
                command.Request.Action) is not { } action
            || action == CommunityLifeModerationState.Flagged)
        {
            return Result.Failure(CommunityLifeErrors.ModerationActionInvalid);
        }

        var applied = await ApplyAsync(
            subjectType, command.SubjectId, action, command.Request.Reason, cancellationToken);

        if (applied.IsFailure)
        {
            return applied;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ApplyAsync(
        ReportSubjectType subjectType,
        Guid subjectId,
        CommunityLifeModerationState action,
        string? reason,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var moderator = _authorization.CallerId;

        if (subjectType == ReportSubjectType.Organization)
        {
            var organization = await _organizations.GetIncludingDeletedAsync(
                OrganizationId.From(subjectId), cancellationToken);

            if (organization is null)
            {
                return Result.Failure(CommunityLifeErrors.OrganizationNotFound);
            }

            switch (action)
            {
                case CommunityLifeModerationState.Hidden:
                    organization.Hide(reason, moderator, now);
                    break;
                case CommunityLifeModerationState.Suspended:
                    organization.Suspend(reason, moderator, now);
                    break;
                default:
                    organization.Clear(moderator, now);
                    break;
            }

            _organizations.Update(organization);

            return Result.Success();
        }

        var communityEvent = await _events.GetIncludingDeletedAsync(
            CommunityEventId.From(subjectId), cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure(CommunityLifeErrors.EventNotFound);
        }

        switch (action)
        {
            case CommunityLifeModerationState.Hidden:
                communityEvent.Hide(reason, moderator, now);
                break;
            case CommunityLifeModerationState.Suspended:
                communityEvent.Suspend(reason, moderator, now);
                break;
            default:
                communityEvent.Clear(moderator, now);
                break;
        }

        _events.Update(communityEvent);

        return Result.Success();
    }
}
