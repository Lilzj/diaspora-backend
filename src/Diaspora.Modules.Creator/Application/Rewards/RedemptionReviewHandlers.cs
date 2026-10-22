using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Financial.Extensibility.Rewards;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Creator.Application.Rewards;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// THE REDEMPTION QUEUE
//
// A redemption used to be marked Redeemed the moment the points were spent, with a grant reference
// built from the reward's own key and nothing anywhere issuing the placement, badge or session it
// named. Members paid and were told they had been given something they never received.
//
// It now stays Pending until a person decides it: fulfil it, recording what was actually issued,
// or reject it, which refunds the points through B15 in the same transaction. Nothing else changes
// a redemption's state, and a decided redemption cannot be decided again.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The fulfilment queue. Pending by default; <c>all</c> for everything.</summary>
[Authorize(Permission = CreatorPermissions.RewardsManage)]
public sealed record ListRedemptionsForReviewQuery : IQuery<IReadOnlyList<CreatorRewardRedemptionResponse>>
{
    public string? State { get; init; }

    public int? Take { get; init; }
}

public sealed class ListRedemptionsForReviewQueryValidator : AbstractValidator<ListRedemptionsForReviewQuery>
{
    public ListRedemptionsForReviewQueryValidator()
    {
        RuleFor(query => query.State)
            .Must(state => ListRedemptionsForReviewQueryHandler.TryParseState(state, out _))
            .WithMessage("State is Pending, Redeemed, Rejected or all.");
    }
}

public sealed class ListRedemptionsForReviewQueryHandler
    : IQueryHandler<ListRedemptionsForReviewQuery, IReadOnlyList<CreatorRewardRedemptionResponse>>
{
    private readonly ICreatorRewardRepository _rewards;

    public ListRedemptionsForReviewQueryHandler(ICreatorRewardRepository rewards) => _rewards = rewards;

    public async Task<Result<IReadOnlyList<CreatorRewardRedemptionResponse>>> Handle(
        ListRedemptionsForReviewQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!TryParseState(query.State, out var state))
        {
            return Result.Failure<IReadOnlyList<CreatorRewardRedemptionResponse>>(
                CreatorErrors.RedemptionStateUnknown);
        }

        var redemptions = await _rewards.ListRedemptionsForReviewAsync(
            state, CreatorLimits.PageSize(query.Take), cancellationToken);

        return Result.Success<IReadOnlyList<CreatorRewardRedemptionResponse>>(
            redemptions.Select(redemption => redemption.ToResponse()).ToList());
    }

    /// <summary>Absent means the queue (Pending); <c>all</c> means no filter.</summary>
    internal static bool TryParseState(string? value, out CreatorRewardState? state)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            state = CreatorRewardState.Pending;
            return true;
        }

        if (string.Equals(value.Trim(), "all", StringComparison.OrdinalIgnoreCase))
        {
            state = null;
            return true;
        }

        var parsed = Enum.TryParse<CreatorRewardState>(value.Trim(), ignoreCase: true, out var known)
            && known is CreatorRewardState.Pending or CreatorRewardState.Redeemed or CreatorRewardState.Rejected;

        state = parsed ? known : null;
        return parsed;
    }
}

/// <summary>
/// Fulfils a pending redemption, or rejects it and refunds the points.
/// <para>
/// <b>A rejection without its refund would be the worst outcome a redemption can have</b> — points
/// gone and nothing given — so the two are one transaction, and a refund B15 will not take fails the
/// rejection rather than recording it. The refund reference is per redemption, so B15 refunds once
/// however the request is retried.
/// </para>
/// <para>
/// A staff member cannot decide their own redemption, for the same reason a grant reviewer cannot
/// decide their own application.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.RewardsManage)]
[Transactional(Reason =
    "Rejecting refunds B15's points and records the rejection. Either alone would leave a member "
    + "with points gone and nothing given, or with both the points and the reward.")]
[Auditable(Action = "creator.reward.decide")]
public sealed record DecideRedemptionCommand : ICommand<CreatorRewardRedemptionResponse>
{
    public required Guid RedemptionId { get; init; }

    public required DecideRedemptionRequest Request { get; init; }
}

public sealed class DecideRedemptionCommandValidator : AbstractValidator<DecideRedemptionCommand>
{
    public DecideRedemptionCommandValidator()
    {
        RuleFor(command => command.RedemptionId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Action).NotEmpty();
        // The column widths: ReferenceLength and SummaryLength.
        RuleFor(command => command.Request.GrantReference).MaximumLength(256);
        RuleFor(command => command.Request.Reason).MaximumLength(1000);
    }
}

public sealed class DecideRedemptionCommandHandler
    : ICommandHandler<DecideRedemptionCommand, CreatorRewardRedemptionResponse>
{
    private const string Fulfil = "fulfil";
    private const string Reject = "reject";

    private readonly CreatorContext _context;
    private readonly ICreatorRewardRepository _rewards;
    private readonly IRewardPointsAccount _points;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public DecideRedemptionCommandHandler(
        CreatorContext context,
        ICreatorRewardRepository rewards,
        IRewardPointsAccount points,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _rewards = rewards;
        _points = points;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CreatorRewardRedemptionResponse>> Handle(
        DecideRedemptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _context.RequireUser();

        if (staff.IsFailure)
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(staff.Error);
        }

        var redemption = await _rewards.GetRedemptionAsync(
            CreatorRewardRedemptionId.From(command.RedemptionId), cancellationToken);

        if (redemption is null)
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(CreatorErrors.RedemptionNotFound);
        }

        if (redemption.UserId == staff.Value)
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(CreatorErrors.ReviewerCannotDecideOwn);
        }

        if (redemption.State != CreatorRewardState.Pending)
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(CreatorErrors.RedemptionAlreadyDecided);
        }

        var now = _clock.UtcNow;
        var request = command.Request;

        switch (request.Action.Trim().ToLowerInvariant())
        {
            case Fulfil when !string.IsNullOrWhiteSpace(request.GrantReference):
                redemption.Fulfil(request.GrantReference, now);
                break;

            case Reject when !string.IsNullOrWhiteSpace(request.Reason):
                var refunded = await _points.RefundAsync(
                    redemption.UserId,
                    redemption.PointsSpent,
                    $"RedemptionRejected:{redemption.RewardKey}",
                    $"creator-redemption-refund:{redemption.Id.Value:N}",
                    cancellationToken);

                // The state check above means this redemption has never been refunded, so a false
                // here is B15 holding no account for the member — not a replay. Recording the
                // rejection anyway would keep the points.
                if (!refunded)
                {
                    return Result.Failure<CreatorRewardRedemptionResponse>(CreatorErrors.RedemptionRefundFailed);
                }

                redemption.Reject(request.Reason, now);
                break;

            default:
                return Result.Failure<CreatorRewardRedemptionResponse>(CreatorErrors.RedemptionDecisionInvalid);
        }

        _rewards.UpdateRedemption(redemption);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(redemption.ToResponse());
    }
}
