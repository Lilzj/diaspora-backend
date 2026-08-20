using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Moderation;

/// <summary>Applies a moderator decision and records it in the append-only history.</summary>
public sealed class DecideModerationCommandHandler
    : ICommandHandler<DecideModerationCommand, ModerationDecisionResponse>
{
    private readonly ModerationService _moderation;
    private readonly ContentReferenceResolver _references;
    private readonly ICurrentUser _currentUser;

    public DecideModerationCommandHandler(
        ModerationService moderation,
        ContentReferenceResolver references,
        ICurrentUser currentUser)
    {
        _moderation = moderation;
        _references = references;
        _currentUser = currentUser;
    }

    public async Task<Result<ModerationDecisionResponse>> Handle(
        DecideModerationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var moderatorId = _currentUser.UserId;

        if (moderatorId is null)
        {
            return Result.Failure<ModerationDecisionResponse>(CommunityErrors.NotAuthenticated);
        }

        var reference = _references.Resolve(
            request.Request.ContentType, request.Request.ContentId);

        if (reference.IsFailure)
        {
            return Result.Failure<ModerationDecisionResponse>(reference.Error);
        }

        var action = CommunityWireNames.Parse<ModerationActionType>(
            request.Request.Action, "moderation action");

        if (action.IsFailure)
        {
            return Result.Failure<ModerationDecisionResponse>(action.Error);
        }

        var decided = await _moderation.DecideAsync(
            reference.Value,
            action.Value,
            moderatorId.Value,
            request.Request.Reason,
            request.Request.ReportId is { } reportId ? ContentReportId.From(reportId) : null,
            cancellationToken);

        return decided.IsFailure
            ? Result.Failure<ModerationDecisionResponse>(decided.Error)
            : Result.Success(ModerationMapper.ToResponse(decided.Value));
    }
}
