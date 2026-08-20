using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
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
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public GetMyReputationQueryHandler(
        IReputationProfileRepository reputations,
        ITrustProfileRepository profiles,
        ICurrentUser currentUser,
        IClock clock)
    {
        _reputations = reputations;
        _profiles = profiles;
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

        return Result.Success(reputation.ToResponse(badges, now));
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
    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RecordConfirmationCommandHandler(
        ICommunityContributionRepository contributions,
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _contributions = contributions;
        _reputation = reputation;
        _provisioner = provisioner;
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

        // The author earns the points, not the confirmer. Ensure they have a profile first —
        // a member can be confirmed before they have ever opened the trust centre.
        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken);

        var outcome = await _reputation.ScoreAsync(
            authorId,
            ReputationActions.CommunityConfirmation,
            request.TargetType,
            request.TargetId,
            request.CountryCode,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ConfirmationResponse
        {
            ConfirmationId = confirmation.Id.Value,
            TargetType = confirmation.TargetType,
            TargetId = confirmation.TargetId,
            PointsAwarded = outcome.WasApplied ? outcome.Points : 0,
            ConfirmedAt = confirmation.ConfirmedAt
        });
    }
}
