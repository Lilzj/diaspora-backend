using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Trust.Application.Reputation;

/// <summary>The caller's reputation, in the shape the reputation screen renders.</summary>
[Authorize(Permission = TrustPermissions.ReadSelf)]
public sealed record GetMyReputationQuery : IQuery<ReputationProfileResponse>;

public sealed class GetMyReputationQueryHandler
    : IQueryHandler<GetMyReputationQuery, ReputationProfileResponse>
{
    private readonly IReputationProfileRepository _reputations;
    private readonly ITrustProfileRepository _profiles;
    private readonly ReputationRuleCatalog _rules;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public GetMyReputationQueryHandler(
        IReputationProfileRepository reputations,
        ITrustProfileRepository profiles,
        ReputationRuleCatalog rules,
        ICurrentUser currentUser,
        IClock clock)
    {
        _reputations = reputations;
        _profiles = profiles;
        _rules = rules;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<ReputationProfileResponse>> Handle(
        GetMyReputationQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<ReputationProfileResponse>(TrustErrors.NotAuthenticated);
        }

        var now = _clock.UtcNow;

        // A member with no scoring history is a Member on zero points, not a 404. The screen
        // must render for a brand new account.
        var reputation = await _reputations.GetByUserReadOnlyAsync(userId, cancellationToken)
            ?? ReputationProfile.Open(userId, now);

        var profile = await _profiles.GetByUserReadOnlyAsync(userId, cancellationToken);

        // Badges are embedded rather than requiring a second call, because the frontend's
        // UserReputation carries them and its screen renders them inline.
        var badges = profile is null
            ? []
            : profile.Badges
                .Where(badge => badge.IsActive)
                .OrderBy(badge => badge.AwardedAt)
                .Select(badge => badge.ToResponse())
                .ToList();

        // From the history, not from the counters: the counters say how often, and only the
        // events say what each occurrence was worth.
        var summaries = await _reputations.SummariseEventsAsync(reputation.Id, cancellationToken);

        return Result.Success(reputation.ToResponse(
            badges, reputation.ToBreakdown(summaries, _rules), now));
    }
}

/// <summary>
/// What the platform pays reputation for: every active rule that something actually scores.
/// <para>
/// Excludes a rule that is switched off (<see cref="ReputationRule.IsActive"/>) and one nothing
/// pays yet (<see cref="ReputationRule.IsAwarded"/>), because a member reads this list as
/// promises. Ordered contribution, verification, trust, and within each the largest award first.
/// </para>
/// </summary>
[Authorize(Permission = TrustPermissions.ReadSelf)]
public sealed record ListReputationRulesQuery : IQuery<IReadOnlyList<ReputationRuleResponse>>;

public sealed class ListReputationRulesQueryHandler
    : IQueryHandler<ListReputationRulesQuery, IReadOnlyList<ReputationRuleResponse>>
{
    private readonly ReputationRuleCatalog _rules;

    public ListReputationRulesQueryHandler(ReputationRuleCatalog rules)
    {
        _rules = rules;
    }

    public Task<Result<IReadOnlyList<ReputationRuleResponse>>> Handle(
        ListReputationRulesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReputationRuleResponse> rules = _rules.All
            .Where(rule => rule.IsActive && rule.IsAwarded)
            .OrderBy(rule => rule.Dimension)
            .ThenByDescending(rule => rule.Points)
            .ThenBy(rule => rule.ActionType, StringComparer.Ordinal)
            .Select(rule => rule.ToResponse(_rules.LabelFor(rule.ActionType)))
            .ToList();

        return Task.FromResult(Result.Success(rules));
    }
}

/// <summary>The caller's scoring history.</summary>
[Authorize(Permission = TrustPermissions.ReadSelf)]
public sealed record ListMyReputationEventsQuery : IQuery<PagedResult<ReputationEventResponse>>
{
    public PaginationRequest Pagination { get; init; } = new();

    public ReputationDimension? Dimension { get; init; }
}

public sealed class ListMyReputationEventsQueryHandler
    : IQueryHandler<ListMyReputationEventsQuery, PagedResult<ReputationEventResponse>>
{
    private readonly IReputationProfileRepository _reputations;
    private readonly ICurrentUser _currentUser;

    public ListMyReputationEventsQueryHandler(
        IReputationProfileRepository reputations, ICurrentUser currentUser)
    {
        _reputations = reputations;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<ReputationEventResponse>>> Handle(
        ListMyReputationEventsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<PagedResult<ReputationEventResponse>>(TrustErrors.NotAuthenticated);
        }

        var page = await _reputations.ListEventsAsync(
            userId, request.Pagination.Sanitize(), request.Dimension, cancellationToken);

        return Result.Success(page.Map(item => item.ToResponse(userId.Value)));
    }
}

/// <summary>
/// Records a community confirmation on someone else's contribution.
/// <para>
/// Replaces the frontend's "community voting" endpoint. Two invariants make the signal worth
/// counting and both are enforced: you cannot confirm your own work (in the aggregate), and
/// you cannot confirm the same thing twice (here, because it needs a repository read).
/// </para>
/// <para>
/// <b>It records; it no longer pays.</b> Everything this command knows about the target —
/// its type, its id, and whose it is — is what the caller typed. Nothing here can check any of
/// it: the content belongs to Guides or Community. Paying on it meant any member holding
/// <c>trust:confirm</c> could name a target that does not exist, name any author, and pay that
/// author five trust points per invented target, without limit; two accounts were enough to farm
/// trust indefinitely.
/// </para>
/// <para>
/// Confirmations are paid where the author is a fact rather than a claim: from a corroborating
/// reaction, through <c>CommunityCorroborationConsumer</c>, which reads the author from the event
/// Community publishes. That is also the only path the app uses — <c>UpdateCard</c> posts the
/// reaction, and nothing calls this route. Its shape is in the Trust contract freeze, so the
/// route stays; <see cref="ConfirmationResponse.PointsAwarded"/> is always zero.
/// </para>
/// </summary>
[Authorize(Permission = TrustPermissions.Confirm)]
[Transactional]
public sealed record RecordConfirmationCommand : ICommand<ConfirmationResponse>
{
    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    public required Guid AuthorUserId { get; init; }

    public string? CountryCode { get; init; }

    public string? Note { get; init; }
}

public sealed class RecordConfirmationCommandValidator : AbstractValidator<RecordConfirmationCommand>
{
    public RecordConfirmationCommandValidator()
    {
        RuleFor(command => command.TargetType)
            .NotEmpty()
            .MaximumLength(CommunityContribution.MaxTargetTypeLength);

        RuleFor(command => command.TargetId)
            .NotEmpty()
            .MaximumLength(CommunityContribution.MaxTargetIdLength);

        RuleFor(command => command.AuthorUserId).NotEmpty();

        RuleFor(command => command.CountryCode)
            .Length(2).When(command => !string.IsNullOrWhiteSpace(command.CountryCode));

        RuleFor(command => command.Note)
            .MaximumLength(CommunityContribution.MaxNoteLength);
    }
}

public sealed class RecordConfirmationCommandHandler
    : ICommandHandler<RecordConfirmationCommand, ConfirmationResponse>
{
    private readonly ICommunityContributionRepository _contributions;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RecordConfirmationCommandHandler(
        ICommunityContributionRepository contributions,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _contributions = contributions;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ConfirmationResponse>> Handle(
        RecordConfirmationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<ConfirmationResponse>(TrustErrors.NotAuthenticated);
        }

        var authorId = UserId.From(request.AuthorUserId);

        if (authorId == userId)
        {
            return Result.Failure<ConfirmationResponse>(TrustErrors.ConfirmationSelfForbidden);
        }

        var already = await _contributions.ExistsAsync(
            userId, request.TargetType, request.TargetId, cancellationToken);

        if (already)
        {
            return Result.Failure<ConfirmationResponse>(TrustErrors.ConfirmationDuplicate);
        }

        var now = _clock.UtcNow;

        CommunityContribution confirmation;
        try
        {
            confirmation = CommunityContribution.Record(
                userId, authorId, request.TargetType, request.TargetId,
                now, request.CountryCode, request.Note);
        }
        catch (DomainException)
        {
            return Result.Failure<ConfirmationResponse>(TrustErrors.ConfirmationSelfForbidden);
        }

        await _contributions.AddAsync(confirmation, cancellationToken);

        // Recorded, so the tally and confirmed-by-viewer stay right — and not scored. See the
        // summary: the author here is the caller's word, and only corroborating reactions pay.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ConfirmationResponse
        {
            ConfirmationId = confirmation.Id.Value,
            TargetType = confirmation.TargetType,
            TargetId = confirmation.TargetId,
            PointsAwarded = 0,
            ConfirmedAt = confirmation.ConfirmedAt
        });
    }
}

/// <summary>
/// How many members confirmed one target, and whether the caller is one of them.
/// <para>
/// The counterpart to <see cref="RecordConfirmationCommand"/>. Reads only — a caller learns
/// the tally for a target it can already see, so this sits on the same read permission as the
/// rest of the trust centre rather than on <c>Confirm</c>.
/// </para>
/// </summary>
[Authorize(Permission = TrustPermissions.ReadSelf)]
public sealed record GetConfirmationSummaryQuery : IQuery<ConfirmationSummaryResponse>
{
    public required string TargetType { get; init; }

    public required string TargetId { get; init; }
}

public sealed class GetConfirmationSummaryQueryValidator
    : AbstractValidator<GetConfirmationSummaryQuery>
{
    public GetConfirmationSummaryQueryValidator()
    {
        RuleFor(query => query.TargetType)
            .NotEmpty()
            .MaximumLength(CommunityContribution.MaxTargetTypeLength);

        RuleFor(query => query.TargetId)
            .NotEmpty()
            .MaximumLength(CommunityContribution.MaxTargetIdLength);
    }
}

public sealed class GetConfirmationSummaryQueryHandler
    : IQueryHandler<GetConfirmationSummaryQuery, ConfirmationSummaryResponse>
{
    private readonly ICommunityContributionRepository _contributions;
    private readonly ICurrentUser _currentUser;

    public GetConfirmationSummaryQueryHandler(
        ICommunityContributionRepository contributions, ICurrentUser currentUser)
    {
        _contributions = contributions;
        _currentUser = currentUser;
    }

    public async Task<Result<ConfirmationSummaryResponse>> Handle(
        GetConfirmationSummaryQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<ConfirmationSummaryResponse>(TrustErrors.NotAuthenticated);
        }

        var count = await _contributions.CountForTargetAsync(
            request.TargetType, request.TargetId, cancellationToken);

        var confirmedByViewer = await _contributions.ExistsAsync(
            userId, request.TargetType, request.TargetId, cancellationToken);

        // A target nobody has confirmed is a zero, not a 404. The control has to render before
        // the first confirmation exists or nobody can ever give it one.
        return Result.Success(new ConfirmationSummaryResponse
        {
            TargetType = request.TargetType.Trim().ToLowerInvariant(),
            TargetId = request.TargetId,
            ConfirmationCount = count,
            ConfirmedByViewer = confirmedByViewer
        });
    }
}
