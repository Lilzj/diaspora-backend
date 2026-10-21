using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Trust.Application.Moderation;

/// <summary>
/// The moderation queue.
/// <para>
/// Ordered by priority then age, so an urgent item that arrived a minute ago outranks a
/// low-priority one from yesterday — which is the only reason to have priorities at all.
/// </para>
/// </summary>
[Authorize(Permission = TrustPermissions.Moderate)]
public sealed record ListModerationQueueQuery : IQuery<PagedResult<ModerationQueueItemResponse>>
{
    public PaginationRequest Pagination { get; init; } = new();

    public ModerationQueueStatus? Status { get; init; }

    public ModerationContentType? ContentType { get; init; }

    public ModerationPriority? MinimumPriority { get; init; }

    public Guid? AssignedTo { get; init; }

    public bool OverdueOnly { get; init; }
}

public sealed class ListModerationQueueQueryHandler
    : IQueryHandler<ListModerationQueueQuery, PagedResult<ModerationQueueItemResponse>>
{
    private readonly IModerationItemRepository _queue;
    private readonly IReputationProfileRepository _reputations;
    private readonly ITrustProfileRepository _profiles;
    private readonly IClock _clock;

    public ListModerationQueueQueryHandler(
        IModerationItemRepository queue,
        IReputationProfileRepository reputations,
        ITrustProfileRepository profiles,
        IClock clock)
    {
        _queue = queue;
        _reputations = reputations;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<Result<PagedResult<ModerationQueueItemResponse>>> Handle(
        ListModerationQueueQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;

        var page = await _queue.ListQueueAsync(
            request.Pagination.Sanitize(),
            request.Status,
            request.ContentType,
            request.MinimumPriority,
            request.AssignedTo is { } assignee ? UserId.From(assignee) : null,
            request.OverdueOnly,
            now,
            cancellationToken);

        // The author's level and violation count are what a moderator triages on — the same
        // two fields the frontend's ModerationQueueItem carries.
        var items = new List<ModerationQueueItemResponse>(page.Items.Count);

        foreach (var item in page.Items)
        {
            var reputation = await _reputations.GetByUserReadOnlyAsync(item.AuthorId, cancellationToken);
            var profile = await _profiles.GetByUserReadOnlyAsync(item.AuthorId, cancellationToken);

            items.Add(item.ToResponse(
                now,
                reputation?.Level.ToString() ?? ReputationLevel.Member.ToString(),
                profile?.ViolationCount ?? 0));
        }

        return Result.Success(PagedResult<ModerationQueueItemResponse>.Create(
            items, page.Page, page.PageSize, page.TotalCount));
    }
}

/// <summary>Takes a queue item onto the caller's desk, or releases it.</summary>
[Authorize(Permission = TrustPermissions.Moderate)]
[Transactional]
public sealed record AssignModerationItemCommand : ICommand<ModerationQueueItemResponse>
{
    public required Guid ItemId { get; init; }

    public bool Assign { get; init; } = true;
}

public sealed class AssignModerationItemCommandValidator
    : AbstractValidator<AssignModerationItemCommand>
{
    public AssignModerationItemCommandValidator()
    {
        RuleFor(command => command.ItemId).NotEmpty();
    }
}

public sealed class AssignModerationItemCommandHandler
    : ICommandHandler<AssignModerationItemCommand, ModerationQueueItemResponse>
{
    private readonly IModerationItemRepository _queue;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AssignModerationItemCommandHandler(
        IModerationItemRepository queue,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _queue = queue;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ModerationQueueItemResponse>> Handle(
        AssignModerationItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var moderatorId = _currentUser.UserId;
        if (moderatorId is null)
        {
            return Result.Failure<ModerationQueueItemResponse>(TrustErrors.NotAuthenticated);
        }

        var item = await _queue.GetAsync(ModerationItemId.From(request.ItemId), cancellationToken);
        if (item is null)
        {
            return Result.Failure<ModerationQueueItemResponse>(TrustErrors.QueueItemNotFound);
        }

        var now = _clock.UtcNow;

        try
        {
            if (request.Assign)
            {
                item.Assign(moderatorId, now);
            }
            else
            {
                item.Release(now);
            }
        }
        catch (DomainException exception) when (exception.Code == "trust.review.self-forbidden")
        {
            return Result.Failure<ModerationQueueItemResponse>(TrustErrors.SelfReviewForbidden);
        }
        catch (DomainException exception)
        {
            return Result.Failure<ModerationQueueItemResponse>(
                Error.Conflict(TrustErrorCodes.QueueItemNotAssigned, exception.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(item.ToResponse(now));
    }
}

/// <summary>
/// Records a moderator's decision.
/// <para>
/// A rejection costs the author trust points and may cost them a badge. Trust does not hide
/// the content itself — it publishes the decision and the owning module acts, because only
/// the module that published a guide knows how to unpublish one.
/// </para>
/// </summary>
[Authorize(Permission = TrustPermissions.Moderate)]
[Transactional]
public sealed record RecordModerationDecisionCommand : ICommand<ModerationDecisionResponse>
{
    public required Guid ItemId { get; init; }

    public required ModerationActionType Action { get; init; }

    public string? Reason { get; init; }
}

public sealed class RecordModerationDecisionCommandValidator
    : AbstractValidator<RecordModerationDecisionCommand>
{
    public RecordModerationDecisionCommandValidator()
    {
        RuleFor(command => command.ItemId).NotEmpty();

        RuleFor(command => command.Action).IsInEnum();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .When(command => command.Action is ModerationActionType.Reject
                or ModerationActionType.Hide or ModerationActionType.Suspend)
            .WithMessage("A reason is required when removing or restricting content.");

        RuleFor(command => command.Reason)
            .MaximumLength(ModerationDecision.MaxReasonLength);
    }
}

public sealed class RecordModerationDecisionCommandHandler
    : ICommandHandler<RecordModerationDecisionCommand, ModerationDecisionResponse>
{
    private readonly IModerationItemRepository _queue;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly ReputationService _reputation;
    private readonly BadgeEvaluator _badges;
    private readonly ITrustEventRepository _timeline;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RecordModerationDecisionCommandHandler(
        IModerationItemRepository queue,
        TrustProfileProvisioner provisioner,
        ReputationService reputation,
        BadgeEvaluator badges,
        ITrustEventRepository timeline,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _queue = queue;
        _provisioner = provisioner;
        _reputation = reputation;
        _badges = badges;
        _timeline = timeline;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ModerationDecisionResponse>> Handle(
        RecordModerationDecisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var moderatorId = _currentUser.UserId;
        if (moderatorId is null)
        {
            return Result.Failure<ModerationDecisionResponse>(TrustErrors.NotAuthenticated);
        }

        var item = await _queue.GetAsync(ModerationItemId.From(request.ItemId), cancellationToken);
        if (item is null)
        {
            return Result.Failure<ModerationDecisionResponse>(TrustErrors.QueueItemNotFound);
        }

        var now = _clock.UtcNow;

        ModerationDecision decision;
        try
        {
            decision = item.Decide(moderatorId, request.Action, request.Reason, now);
        }
        catch (DomainException exception) when (exception.Code == "trust.review.self-forbidden")
        {
            return Result.Failure<ModerationDecisionResponse>(TrustErrors.SelfReviewForbidden);
        }
        catch (DomainException exception)
        {
            return Result.Failure<ModerationDecisionResponse>(
                Error.Conflict(TrustErrorCodes.QueueItemNotAssigned, exception.Message));
        }

        // A removal is a trust event for the author, not merely a content change.
        if (request.Action is ModerationActionType.Reject or ModerationActionType.Hide
            or ModerationActionType.Suspend)
        {
            var (profile, reputationProfile) = await _provisioner.GetOrCreateBothAsync(
                item.AuthorId, cancellationToken);

            if (request.Action == ModerationActionType.Suspend)
            {
                profile.RecordViolation(request.Reason ?? "Content policy violation.", now);

                await _reputation.ScoreAsync(
                    item.AuthorId,
                    ReputationActions.ViolationRecorded,
                    ReputationAwardKey.Moderation(ReputationActions.ViolationRecorded, item.Id.Value),
                    item.ContentType.ToString(),
                    item.ContentId,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await _reputation.ScoreAsync(
                    item.AuthorId,
                    ReputationActions.ContentRemoved,
                    ReputationAwardKey.Moderation(ReputationActions.ContentRemoved, item.Id.Value),
                    item.ContentType.ToString(),
                    item.ContentId,
                    cancellationToken: cancellationToken);
            }

            _badges.Reconcile(profile, reputationProfile, profile.HasBadge(BadgeType.Moderator), now);

            await _timeline.AddAsync(
                TrustEvent.Record(
                    item.AuthorId,
                    TrustEventTypes.Violation,
                    "Content moderated",
                    now,
                    request.Reason,
                    TrustEventTypes.IconFor(TrustEventTypes.Violation)),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        TrustTelemetrySource.ModerationDecisions.Add(
            1,
            new KeyValuePair<string, object?>("contentType", item.ContentType.ToString()),
            new KeyValuePair<string, object?>("action", request.Action.ToString()));

        return Result.Success(decision.ToResponse(item.ContentType, item.ContentId));
    }
}
