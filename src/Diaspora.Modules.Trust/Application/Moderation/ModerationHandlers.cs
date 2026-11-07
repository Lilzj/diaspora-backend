using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Trust.Application.Moderation;

// ═════════════════════════════════════════════════════════════════════════════════════════════
// The moderation case loop (7 Nov 2026).
//
// Every staff action on a member's content is a decision on a case here. A case comes from
// members' reports, or from staff with a reason and a note. A decision carries a reason code and
// a note the owner reads; it is made once; a removal needs a second member of staff; an emergency
// hide and a staff-opened case are checked by one. The owning module carries the decision out
// through IModerationTarget, in the same transaction.
// ═════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The moderation queue: listed cases, priority first, then age.</summary>
[Authorize(Permission = TrustPermissions.Moderate)]
public sealed record ListModerationQueueQuery : IQuery<PagedResult<ModerationQueueItemResponse>>
{
    public PaginationRequest Pagination { get; init; } = new();

    public ModerationQueueStatus? Status { get; init; }

    public ModerationContentType? ContentType { get; init; }

    /// <summary>A module content type: job, housing-listing, guide, and so on.</summary>
    public string? TargetType { get; init; }

    public ModerationPriority? MinimumPriority { get; init; }

    public Guid? AssignedTo { get; init; }

    public bool OverdueOnly { get; init; }

    /// <summary>Only cases waiting for a second member of staff.</summary>
    public bool AwaitingSecondReview { get; init; }
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
            request.TargetType,
            request.AwaitingSecondReview,
            cancellationToken);

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

/// <summary>One case in full: its reports (without reporters), and every decision on it.</summary>
[Authorize(Permission = TrustPermissions.Moderate)]
public sealed record GetModerationCaseQuery : IQuery<ModerationQueueItemResponse>
{
    public Guid? ItemId { get; init; }

    /// <summary>With <see cref="ContentId"/>: the latest case about this content.</summary>
    public string? TargetType { get; init; }

    public Guid? ContentId { get; init; }
}

public sealed class GetModerationCaseQueryHandler
    : IQueryHandler<GetModerationCaseQuery, ModerationQueueItemResponse>
{
    private readonly IModerationItemRepository _queue;
    private readonly IClock _clock;

    public GetModerationCaseQueryHandler(IModerationItemRepository queue, IClock clock)
    {
        _queue = queue;
        _clock = clock;
    }

    public async Task<Result<ModerationQueueItemResponse>> Handle(
        GetModerationCaseQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = request.ItemId is { } id
            ? await _queue.GetAsync(ModerationItemId.From(id), cancellationToken)
            : request.TargetType is { } type && request.ContentId is { } content
                ? await _queue.GetLatestForTargetAsync(type, content.ToString(), cancellationToken)
                : null;

        return item is null
            ? Result.Failure<ModerationQueueItemResponse>(TrustErrors.QueueItemNotFound)
            : Result.Success(item.ToResponse(_clock.UtcNow));
    }
}

/// <summary>Takes a case onto the caller's desk, or releases it.</summary>
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
        catch (DomainException exception)
        {
            return Result.Failure<ModerationQueueItemResponse>(ModerationCaseErrors.From(exception));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(item.ToResponse(now));
    }
}

/// <summary>
/// A moderator's decision on a case: no action, hide pending review, remove (a proposal a second
/// member of staff confirms), or restore. Carried out on the content in the same transaction.
/// </summary>
[Authorize(Permission = TrustPermissions.Moderate)]
[Transactional]
[Auditable(Action = "trust.case.decided")]
public sealed record RecordModerationDecisionCommand : ICommand<ModerationQueueItemResponse>
{
    public required Guid ItemId { get; init; }

    public required ModerationOutcome Outcome { get; init; }

    public required ModerationReasonCode ReasonCode { get; init; }

    public required string Note { get; init; }
}

public sealed class RecordModerationDecisionCommandValidator
    : AbstractValidator<RecordModerationDecisionCommand>
{
    public RecordModerationDecisionCommandValidator()
    {
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.Outcome).IsInEnum();
        RuleFor(command => command.ReasonCode).IsInEnum();
        RuleFor(command => command.Note).ModerationNote();
    }
}

public sealed class RecordModerationDecisionCommandHandler
    : ICommandHandler<RecordModerationDecisionCommand, ModerationQueueItemResponse>
{
    private readonly IModerationItemRepository _queue;
    private readonly ModerationTargetRegistry _targets;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RecordModerationDecisionCommandHandler(
        IModerationItemRepository queue,
        ModerationTargetRegistry targets,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _queue = queue;
        _targets = targets;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ModerationQueueItemResponse>> Handle(
        RecordModerationDecisionCommand request, CancellationToken cancellationToken)
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

        var target = ModerationCaseTargets.Resolve(_targets, item);
        if (target.IsFailure)
        {
            return Result.Failure<ModerationQueueItemResponse>(target.Error);
        }

        var now = _clock.UtcNow;
        ModerationOutcome? apply;

        try
        {
            (_, apply) = item.Decide(moderatorId, request.Outcome, request.ReasonCode, request.Note, now);
        }
        catch (DomainException exception)
        {
            return Result.Failure<ModerationQueueItemResponse>(ModerationCaseErrors.From(exception));
        }

        if (apply is { } outcome)
        {
            var applied = await ModerationCaseTargets.ApplyAsync(
                target.Value, item, outcome, moderatorId, request.ReasonCode, request.Note, cancellationToken);

            if (applied.IsFailure)
            {
                return Result.Failure<ModerationQueueItemResponse>(applied.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        TrustTelemetrySource.ModerationDecisions.Add(
            1,
            new KeyValuePair<string, object?>("contentType", item.TargetType ?? item.ContentType.ToString()),
            new KeyValuePair<string, object?>("action", request.Outcome.ToString()));

        return Result.Success(item.ToResponse(now));
    }
}

/// <summary>
/// A second member of staff checks the first one's decision: a proposed removal, an emergency hide,
/// or a case staff opened with no report.
/// </summary>
[Authorize(Permission = TrustPermissions.Moderate)]
[Transactional]
[Auditable(Action = "trust.case.second-review")]
public sealed record CompleteSecondReviewCommand : ICommand<ModerationQueueItemResponse>
{
    public required Guid ItemId { get; init; }

    public required bool Confirm { get; init; }

    public ModerationOutcome? Instead { get; init; }

    public required string Note { get; init; }
}

public sealed class CompleteSecondReviewCommandValidator : AbstractValidator<CompleteSecondReviewCommand>
{
    public CompleteSecondReviewCommandValidator()
    {
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.Instead).IsInEnum().When(command => command.Instead is not null);
        RuleFor(command => command.Note).ModerationNote();
    }
}

public sealed class CompleteSecondReviewCommandHandler
    : ICommandHandler<CompleteSecondReviewCommand, ModerationQueueItemResponse>
{
    private readonly IModerationItemRepository _queue;
    private readonly ModerationTargetRegistry _targets;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CompleteSecondReviewCommandHandler(
        IModerationItemRepository queue,
        ModerationTargetRegistry targets,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _queue = queue;
        _targets = targets;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ModerationQueueItemResponse>> Handle(
        CompleteSecondReviewCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reviewerId = _currentUser.UserId;
        if (reviewerId is null)
        {
            return Result.Failure<ModerationQueueItemResponse>(TrustErrors.NotAuthenticated);
        }

        var item = await _queue.GetAsync(ModerationItemId.From(request.ItemId), cancellationToken);
        if (item is null)
        {
            return Result.Failure<ModerationQueueItemResponse>(TrustErrors.QueueItemNotFound);
        }

        var target = ModerationCaseTargets.Resolve(_targets, item);
        if (target.IsFailure)
        {
            return Result.Failure<ModerationQueueItemResponse>(target.Error);
        }

        var now = _clock.UtcNow;
        ModerationDecision decision;
        ModerationOutcome? apply;

        try
        {
            (decision, apply) = item.CompleteSecondReview(
                reviewerId, request.Confirm, request.Instead, request.Note, now);
        }
        catch (DomainException exception)
        {
            return Result.Failure<ModerationQueueItemResponse>(ModerationCaseErrors.From(exception));
        }

        if (apply is { } outcome)
        {
            var applied = await ModerationCaseTargets.ApplyAsync(
                target.Value,
                item,
                outcome,
                reviewerId,
                decision.ReasonCode ?? ModerationReasonCode.Other,
                decision.Reason ?? request.Note,
                cancellationToken);

            if (applied.IsFailure)
            {
                return Result.Failure<ModerationQueueItemResponse>(applied.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(item.ToResponse(now));
    }
}

/// <summary>
/// Staff open a case with no report, because staff do find scams first. Listed at once, and
/// checked by a second member of staff within seven days.
/// </summary>
[Authorize(Permission = TrustPermissions.Moderate)]
[Transactional]
[Auditable(Action = "trust.case.opened-by-staff")]
public sealed record OpenModerationCaseCommand : ICommand<ModerationQueueItemResponse>
{
    public required string TargetType { get; init; }

    public required Guid ContentId { get; init; }

    public required ModerationReasonCode ReasonCode { get; init; }

    public required string Note { get; init; }
}

public sealed class OpenModerationCaseCommandValidator : AbstractValidator<OpenModerationCaseCommand>
{
    public OpenModerationCaseCommandValidator()
    {
        RuleFor(command => command.TargetType).NotEmpty().MaximumLength(ModerationItem.MaxTargetTypeLength);
        RuleFor(command => command.ContentId).NotEmpty();
        RuleFor(command => command.ReasonCode).IsInEnum();
        RuleFor(command => command.Note).ModerationNote();
    }
}

public sealed class OpenModerationCaseCommandHandler
    : ICommandHandler<OpenModerationCaseCommand, ModerationQueueItemResponse>
{
    private readonly IModerationItemRepository _queue;
    private readonly ModerationTargetRegistry _targets;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public OpenModerationCaseCommandHandler(
        IModerationItemRepository queue,
        ModerationTargetRegistry targets,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _queue = queue;
        _targets = targets;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ModerationQueueItemResponse>> Handle(
        OpenModerationCaseCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var staffId = _currentUser.UserId;
        if (staffId is null)
        {
            return Result.Failure<ModerationQueueItemResponse>(TrustErrors.NotAuthenticated);
        }

        var target = _targets.Find(request.TargetType);
        if (target is null)
        {
            return Result.Failure<ModerationQueueItemResponse>(ModerationCaseErrors.NoModule(request.TargetType));
        }

        var snapshot = await target.DescribeAsync(request.ContentId, cancellationToken);
        if (snapshot is null)
        {
            return Result.Failure<ModerationQueueItemResponse>(ModerationCaseErrors.ContentNotFound);
        }

        var open = await _queue.GetOpenForTargetAsync(
            target.ContentType, request.ContentId.ToString(), cancellationToken);

        if (open is not null)
        {
            return Result.Failure<ModerationQueueItemResponse>(ModerationCaseErrors.AlreadyOpen(open.Id.Value));
        }

        ModerationItem item;

        try
        {
            item = ModerationItem.OpenByStaff(
                target.ContentType,
                target.Area,
                request.ContentId,
                UserId.From(snapshot.OwnerUserId),
                snapshot.Title,
                staffId,
                request.ReasonCode,
                request.Note,
                _clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure<ModerationQueueItemResponse>(ModerationCaseErrors.From(exception));
        }

        await _queue.AddAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(item.ToResponse(_clock.UtcNow));
    }
}

/// <summary>
/// The owner fixed what was hidden and asks a moderator to look again. Open to any member, for
/// their own content only.
/// </summary>
[Authorize(Permission = TrustPermissions.ReadSelf)]
[Transactional]
[Auditable(Action = "trust.case.review-requested")]
public sealed record RequestCaseReviewCommand : ICommand
{
    public required Guid ItemId { get; init; }
}

public sealed class RequestCaseReviewCommandHandler : ICommandHandler<RequestCaseReviewCommand>
{
    private readonly IModerationItemRepository _queue;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RequestCaseReviewCommandHandler(
        IModerationItemRepository queue, ICurrentUser currentUser, IUnitOfWork unitOfWork, IClock clock)
    {
        _queue = queue;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(RequestCaseReviewCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownerId = _currentUser.UserId;
        if (ownerId is null)
        {
            return Result.Failure(TrustErrors.NotAuthenticated);
        }

        var item = await _queue.GetAsync(ModerationItemId.From(request.ItemId), cancellationToken);

        // Somebody else's case answers as missing: whether a case exists about content that is not
        // yours is not yours to know.
        if (item is null || item.AuthorId != ownerId)
        {
            return Result.Failure(TrustErrors.QueueItemNotFound);
        }

        try
        {
            item.RequestReview(ownerId, _clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure(ModerationCaseErrors.From(exception));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// ── Shared ─────────────────────────────────────────────────────────────────────────────────────

internal static class ModerationNoteRules
{
    public static IRuleBuilderOptions<T, string> ModerationNote<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(note => (note?.Trim().Length ?? 0) >= ModerationPolicy.MinimumNoteLength)
            .WithMessage(
                $"A decision needs a note of at least {ModerationPolicy.MinimumNoteLength} characters: it is what the owner reads.")
            .MaximumLength(ModerationPolicy.MaximumNoteLength);
}

internal static class ModerationCaseTargets
{
    public static Result<IModerationTarget> Resolve(ModerationTargetRegistry targets, ModerationItem item) =>
        targets.Find(item.TargetType) is { } target
            ? Result.Success(target)
            : Result.Failure<IModerationTarget>(ModerationCaseErrors.NoModule(item.TargetType));

    public static async Task<Result> ApplyAsync(
        IModerationTarget target,
        ModerationItem item,
        ModerationOutcome outcome,
        UserId actor,
        ModerationReasonCode code,
        string note,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(item.ContentId, out var contentId))
        {
            return Result.Failure(ModerationCaseErrors.ContentNotFound);
        }

        var applied = await target.ApplyAsync(
            contentId, outcome, new ModerationActor(actor.Value, item.Id.Value, code, note), cancellationToken);

        return applied.IsFailure ? Result.Failure(applied.Error) : Result.Success();
    }
}

/// <summary>The case loop's refusals, by the domain's codes.</summary>
public static class ModerationCaseErrors
{
    public static readonly Error ContentNotFound = Error.NotFound(
        "trust.case.content-not-found", "The content this case is about no longer exists.");

    public static Error NoModule(string? targetType) => Error.Validation(
        "trust.case.no-module",
        $"No module moderates '{targetType ?? "this content"}', so a decision on it could not be carried out.");

    public static Error AlreadyOpen(Guid itemId) => Error.Conflict(
        "trust.case.already-open",
        $"A case is already open for this content ({itemId}). Work on that one.");

    public static Error From(DomainException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.Code switch
        {
            "trust.review.self-forbidden" => TrustErrors.SelfReviewForbidden,
            "trust.case.same-reviewer" => Error.Forbidden(exception.Code, exception.Message),
            "trust.case.not-owner" => Error.Forbidden(exception.Code, exception.Message),
            "trust.case.note-required" or "trust.case.outcome-invalid"
                => Error.Validation(exception.Code, exception.Message),
            { Length: > 0 } code => Error.Conflict(code, exception.Message),
            _ => Error.Conflict(TrustErrorCodes.QueueItemNotAssigned, exception.Message)
        };
    }
}
