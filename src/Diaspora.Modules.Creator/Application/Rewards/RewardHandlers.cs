using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Domain.Rewards;
using Diaspora.Modules.Creator.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Rewards;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Creator.Application.Rewards;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-010 and -011 — the sign-up bonus, the rules catalogue and redemptions.
//
// The one thing to hold on to reading this file: **this module owns no points balance.** B15's
// RewardAccount is the platform's single points ledger. Awarding publishes
// RewardPointsGrantedIntegrationEvent, which B15 already consumes; spending goes through B15's
// IRewardPointsAccount, which debits inside the caller's unit of work. There is no balance column
// anywhere in the creator schema and an architecture test asserts it.
//
// §31 of the phase brief: "A malicious client must not be able to POST /rewards/redemptions with
// points = 999999 and influence the outcome." The request carries a reward identifier and a key.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The caller's sign-up bonus, as a state.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetMySignupBonusQuery : IQuery<SignupBonusResponse>;

public sealed class GetMySignupBonusQueryHandler
    : IQueryHandler<GetMySignupBonusQuery, SignupBonusResponse>
{
    private readonly CreatorContext _context;
    private readonly ISignupBonusRepository _bonuses;
    private readonly SignupBonusEligibility _eligibility;

    public GetMySignupBonusQueryHandler(
        CreatorContext context,
        ISignupBonusRepository bonuses,
        SignupBonusEligibility eligibility)
    {
        _context = context;
        _bonuses = bonuses;
        _eligibility = eligibility;
    }

    public async Task<Result<SignupBonusResponse>> Handle(
        GetMySignupBonusQuery query, CancellationToken cancellationToken)
    {
        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<SignupBonusResponse>(caller.Error);
        }

        var bonus = await _bonuses.FindForUserAsync(caller.Value, cancellationToken);

        if (bonus is null)
        {
            // No row means the platform never offered one to this member — a member who registered
            // before the programme existed. NotEligible is the honest answer and carries the same
            // member-safe sentence every other refusal does.
            return Result.Success(new SignupBonusResponse
            {
                State = SignupBonusState.NotEligible.ToString(),
                Points = 0,
                IneligibleReason = "This bonus is not available on your account.",
                Requirements = [],
                AwardedAt = null,
                ExpiresAt = null
            });
        }

        return Result.Success(bonus.ToResponse(_eligibility.Requirements()));
    }
}

/// <summary>
/// Acknowledges the sign-up bonus.
/// <para>
/// <b>Acknowledging is not claiming.</b> The register: "There is deliberately no <c>claim</c> that
/// credits anything on the device — <c>acknowledgeSignupBonus</c> tells the server the member
/// accepted, and the server decides what that is worth." The server re-checks the outstanding
/// requirements here rather than trusting that the client checked before calling.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.signup-bonus.acknowledge")]
public sealed record AcknowledgeSignupBonusCommand : ICommand<SignupBonusResponse>
{
    public AcknowledgeSignupBonusRequest? Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class AcknowledgeSignupBonusCommandHandler
    : ICommandHandler<AcknowledgeSignupBonusCommand, SignupBonusResponse>
{
    private const string Operation = "creator-signup-bonus-acknowledge";

    private readonly CreatorContext _context;
    private readonly ISignupBonusRepository _bonuses;
    private readonly SignupBonusEligibility _eligibility;
    private readonly IUnitOfWork _unitOfWork;

    public AcknowledgeSignupBonusCommandHandler(
        CreatorContext context,
        ISignupBonusRepository bonuses,
        SignupBonusEligibility eligibility,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _bonuses = bonuses;
        _eligibility = eligibility;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SignupBonusResponse>> Handle(
        AcknowledgeSignupBonusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<SignupBonusResponse>(creator.Error);
        }

        var key = CreatorIdempotency.Normalise(
            Operation, creator.Value.UserId, command.IdempotencyKeyHeader, command.Request?.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _bonuses.FindByAcknowledgeKeyAsync(creator.Value.Id, key, cancellationToken);

            if (replayed is not null)
            {
                return Result.Success(replayed.ToResponse(_eligibility.Requirements()));
            }
        }

        var bonus = await _bonuses.FindForUserAsync(creator.Value.UserId, cancellationToken);

        if (bonus is null)
        {
            return Result.Failure<SignupBonusResponse>(CreatorErrors.SignupBonusNotEligible);
        }

        var requirements = _eligibility.Requirements();

        // A risk refusal is applied here rather than trusted from the row, so a member whose
        // standing changed since the bonus was offered is refused now. The reason stored on the row
        // and the reason returned are the same single sentence, and neither names a check.
        if (SignupBonusEligibility.IsBlocked(creator.Value))
        {
            bonus.Block("This bonus is not available on your account.", DateTimeOffset.UtcNow);
            _bonuses.Update(bonus);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(bonus.ToResponse(requirements));
        }

        var reference = $"signup-bonus:{bonus.Id.Value:N}";

        bonus.Acknowledge(requirements.All(requirement => requirement.IsMet), reference, key,
            DateTimeOffset.UtcNow);

        _bonuses.Update(bonus);

        var commit = await CreatorIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => _bonuses.FindByAcknowledgeKeyAsync(creator.Value.Id, key!, token),
            cancellationToken);

        if (commit.IsFailure)
        {
            return Result.Failure<SignupBonusResponse>(commit.Error);
        }

        return Result.Success((commit.Value.Replayed ?? bonus).ToResponse(requirements));
    }
}

/// <summary>
/// What a member must do before the sign-up bonus is awarded, and whether risk blocks it.
/// <para>
/// <b>The block's reason never names the check.</b> The register asserts this on the client side;
/// this is the same rule on the server, and the integration suite asserts the response body carries
/// no signal.
/// </para>
/// </summary>
public sealed class SignupBonusEligibility
{
    private readonly CreatorContext _context;

    public SignupBonusEligibility(CreatorContext context) => _context = context;

    /// <summary>
    /// The outstanding requirements, evaluated against real state.
    /// <para>
    /// <b>Finishing onboarding, and nothing else.</b> A sign-up bonus that required identity
    /// verification would be a sign-up bonus almost nobody could collect on signing up, and the
    /// access-tier specification already records the platform's position: "a welcome bonus or a
    /// referral credit can land before verification". So the bar is the one the bonus is named
    /// after — verify your phone number and you are in.
    /// </para>
    /// <para>
    /// <b><see cref="SignupBonusState.Pending"/> is reachable because of this.</b> The bonus read
    /// sits at the Onboarding tier, so somebody halfway through registering can see it and
    /// acknowledge it; the award waits until they finish. A requirement nobody could fail would make
    /// Pending dead code and the state list a fiction.
    /// </para>
    /// </summary>
    public IReadOnlyList<CreatorRequirement> Requirements()
    {
        var onboarded = _context.IsOnboarded();

        return
        [
            CreatorRequirement.Flag(
                "onboarding-complete",
                "Finish setting up your account",
                onboarded,
                "Verify your phone number to collect your bonus.",
                "/verification/phone",
                "Verify phone")
        ];
    }

    /// <summary>
    /// Whether the platform is withholding the bonus.
    /// <para>
    /// Today this is the account's standing and nothing else — this module runs no fraud engine and
    /// declaring one it does not have would be the <c>IEligibilityService</c> mistake PSR-1.2
    /// recorded. When a risk service exists, this is the one method that consults it.
    /// </para>
    /// </summary>
    public static bool IsBlocked(Domain.Creators.CreatorProfile creator)
    {
        ArgumentNullException.ThrowIfNull(creator);

        return creator.IsSuspended || creator.IsRestricted;
    }
}

/// <summary>CREATOR-GAP-011. The rules catalogue, with its version and expiry policy.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetRewardRulesQuery : IQuery<CreatorPointRulesResponse>;

public sealed class GetRewardRulesQueryHandler
    : IQueryHandler<GetRewardRulesQuery, CreatorPointRulesResponse>
{
    private readonly ICreatorRewardRepository _rewards;
    private readonly IClock _clock;

    public GetRewardRulesQueryHandler(ICreatorRewardRepository rewards, IClock clock)
    {
        _rewards = rewards;
        _clock = clock;
    }

    public async Task<Result<CreatorPointRulesResponse>> Handle(
        GetRewardRulesQuery query, CancellationToken cancellationToken)
    {
        var rules = await _rewards.ListRulesAsync(cancellationToken);

        return Result.Success(new CreatorPointRulesResponse
        {
            Rules = rules.Select(rule => rule.ToResponse()).ToList(),
            CatalogueVersion = CreatorRewardCatalogueVersion.Current,
            ExpiryPolicy = CreatorRewardCatalogueVersion.ExpiryPolicy.ToString(),
            ExpiryDescription = CreatorRewardCatalogueVersion.ExpiryDescription,
            UpdatedAt = rules.Count == 0
                ? _clock.UtcNow
                : rules.Max(rule => rule.ModifiedAt ?? rule.CreatedAt)
        });
    }
}

/// <summary>
/// CREATOR-GAP-011. The redemption catalogue, with each reward's state decided for this caller.
/// <para>
/// <b>The balance comes from B15 and is never held here.</b> Serving it alongside the catalogue is
/// what lets the client show "you need 200 more" without a second round trip and without ever
/// holding a figure it could decrement.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetRewardCatalogueQuery : IQuery<CreatorRewardCatalogueResponse>;

public sealed class GetRewardCatalogueQueryHandler
    : IQueryHandler<GetRewardCatalogueQuery, CreatorRewardCatalogueResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorRewardRepository _rewards;
    private readonly IRewardPointsAccount _points;
    private readonly IClock _clock;

    public GetRewardCatalogueQueryHandler(
        CreatorContext context,
        ICreatorRewardRepository rewards,
        IRewardPointsAccount points,
        IClock clock)
    {
        _context = context;
        _rewards = rewards;
        _points = points;
        _clock = clock;
    }

    public async Task<Result<CreatorRewardCatalogueResponse>> Handle(
        GetRewardCatalogueQuery query, CancellationToken cancellationToken)
    {
        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorRewardCatalogueResponse>(caller.Error);
        }

        var now = _clock.UtcNow;
        var rewards = await _rewards.ListRewardsAsync(cancellationToken);

        // A member with no creator record yet is a NotACreator creator, not somebody below every
        // rung. Treating the absence as ineligible produced "Open to NotACreator and above" — copy
        // that is both nonsense and wrong, and hid the real reason, which is the balance.
        var persisted = await _context.FindCreatorAsync(cancellationToken);
        var creator = persisted ?? _context.TransientProfileFor(caller.Value);

        var balance = await _points.GetBalanceAsync(caller.Value, cancellationToken);

        var response = new List<CreatorRewardResponse>(rewards.Count);

        foreach (var reward in rewards)
        {
            var (state, reason) = await ResolveAsync(
                reward, creator, persisted is not null, balance, now, cancellationToken);

            response.Add(reward.ToResponse(state, reason));
        }

        return Result.Success(new CreatorRewardCatalogueResponse
        {
            Rewards = response,
            CatalogueVersion = CreatorRewardCatalogueVersion.Current,
            AvailablePoints = balance.Available,
            UpdatedAt = now
        });
    }

    private async Task<(CreatorRewardState State, string? Reason)> ResolveAsync(
        CreatorRewardDefinition reward,
        Domain.Creators.CreatorProfile creator,
        bool isPersisted,
        RewardPointsBalance balance,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!reward.IsOnOffer(now))
        {
            return (CreatorRewardState.Expired, "This reward is no longer on offer.");
        }

        if (creator.EarnedStatus < reward.MinimumStatus)
        {
            return (CreatorRewardState.Locked, $"Open to {reward.MinimumStatus} and above.");
        }

        if (creator.IsSuspended || creator.IsRestricted)
        {
            return (CreatorRewardState.Locked,
                "Your account cannot redeem rewards at the moment.");
        }

        // A member with no record has redeemed nothing, so the cap and the cooldown cannot bite —
        // and asking the database about a transient identifier would be two queries for a row that
        // by construction does not exist.
        if (isPersisted && reward.MaxPerCreator is { } cap)
        {
            var used = await _rewards.CountRedemptionsAsync(creator.Id, reward.Id, cancellationToken);

            if (used >= cap)
            {
                return (CreatorRewardState.Locked, "You have redeemed this as many times as you can.");
            }
        }

        if (isPersisted && reward.CooldownHours is { } cooldown)
        {
            var last = await _rewards.LastRedeemedAtAsync(creator.Id, reward.Id, cancellationToken);

            if (last is { } at && at.AddHours(cooldown) > now)
            {
                return (CreatorRewardState.Locked, "You have redeemed this recently.");
            }
        }

        return balance.Available < reward.PointsCost
            ? (CreatorRewardState.Locked,
                $"You need {reward.PointsCost - balance.Available} more points.")
            : (CreatorRewardState.Available, null);
    }
}

/// <summary>The caller's redemptions.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record ListMyRedemptionsQuery : IQuery<IReadOnlyList<CreatorRewardRedemptionResponse>>
{
    public int? Take { get; init; }
}

public sealed class ListMyRedemptionsQueryHandler
    : IQueryHandler<ListMyRedemptionsQuery, IReadOnlyList<CreatorRewardRedemptionResponse>>
{
    private readonly CreatorContext _context;
    private readonly ICreatorRewardRepository _rewards;

    public ListMyRedemptionsQueryHandler(
        CreatorContext context, ICreatorRewardRepository rewards)
    {
        _context = context;
        _rewards = rewards;
    }

    public async Task<Result<IReadOnlyList<CreatorRewardRedemptionResponse>>> Handle(
        ListMyRedemptionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CreatorRewardRedemptionResponse>>(caller.Error);
        }

        var creator = await _context.FindCreatorAsync(cancellationToken);

        if (creator is null)
        {
            return Result.Success<IReadOnlyList<CreatorRewardRedemptionResponse>>([]);
        }

        var redemptions = await _rewards.ListRedemptionsAsync(
            creator.Id, query.Take ?? 20, cancellationToken);

        return Result.Success<IReadOnlyList<CreatorRewardRedemptionResponse>>(
            redemptions.Select(redemption => redemption.ToResponse()).ToList());
    }
}

/// <summary>
/// Redeems a reward.
/// <para>
/// <b>The price is the catalogue's and the debit is B15's, in one transaction.</b> Checking a
/// balance and then publishing a "points spent" event would leave a window in which two concurrent
/// redemptions both saw enough points; reading and debiting through
/// <see cref="IRewardPointsAccount"/> inside this unit of work makes the check and the debit one
/// decision, and B15's unique index on the spend reference decides the race if two get that far.
/// </para>
/// <para>
/// <b>Every eligibility rule is re-checked here.</b> The catalogue read's <c>Locked</c> state is a
/// courtesy to the client; this is the decision.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Transactional(Reason =
    "Debits B15's points balance and writes the redemption. A debit that committed without its "
    + "redemption would take points for nothing.")]
[Auditable(Action = "creator.reward.redeem")]
public sealed record RedeemRewardCommand : ICommand<CreatorRewardRedemptionResponse>
{
    public required RedeemRewardRequest Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class RedeemRewardCommandValidator : AbstractValidator<RedeemRewardCommand>
{
    public RedeemRewardCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.RewardId).NotEmpty();
    }
}

public sealed class RedeemRewardCommandHandler
    : ICommandHandler<RedeemRewardCommand, CreatorRewardRedemptionResponse>
{
    private const string Operation = "creator-reward-redeem";

    private readonly CreatorContext _context;
    private readonly ICreatorRewardRepository _rewards;
    private readonly IRewardPointsAccount _points;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RedeemRewardCommandHandler(
        CreatorContext context,
        ICreatorRewardRepository rewards,
        IRewardPointsAccount points,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _context = context;
        _rewards = rewards;
        _points = points;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<CreatorRewardRedemptionResponse>> Handle(
        RedeemRewardCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(allowed.Error);
        }

        var key = CreatorIdempotency.Normalise(
            Operation, creator.Value.UserId, command.IdempotencyKeyHeader, command.Request.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _rewards.FindByIdempotencyKeyAsync(creator.Value.Id, key, cancellationToken);

            if (replayed is not null)
            {
                // The same key for a different reward is a client bug, not a retry. Replaying the
                // first redemption would tell the member they got something they did not ask for.
                return replayed.RewardId == CreatorRewardId.From(command.Request.RewardId)
                    ? Result.Success(replayed.ToResponse())
                    : Result.Failure<CreatorRewardRedemptionResponse>(CreatorErrors.IdempotencyKeyReused);
            }
        }

        var reward = await _rewards.GetRewardAsync(
            CreatorRewardId.From(command.Request.RewardId), cancellationToken);

        if (reward is null || !reward.IsActive)
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(CreatorErrors.RewardNotFound);
        }

        var now = _clock.UtcNow;

        if (!reward.IsOnOffer(now))
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(CreatorErrors.RewardNotAvailable);
        }

        if (creator.Value.EarnedStatus < reward.MinimumStatus)
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(CreatorErrors.RewardNotAvailable);
        }

        if (reward.MaxPerCreator is { } cap)
        {
            var used = await _rewards.CountRedemptionsAsync(
                creator.Value.Id, reward.Id, cancellationToken);

            if (used >= cap)
            {
                return Result.Failure<CreatorRewardRedemptionResponse>(
                    CreatorErrors.RewardLimitReached);
            }
        }

        if (reward.CooldownHours is { } cooldown)
        {
            var last = await _rewards.LastRedeemedAtAsync(
                creator.Value.Id, reward.Id, cancellationToken);

            if (last is { } at && at.AddHours(cooldown) > now)
            {
                return Result.Failure<CreatorRewardRedemptionResponse>(CreatorErrors.RewardCooldown);
            }
        }

        // The redemption identifier is minted first so it can be the debit's reference — which makes
        // the debit idempotent on the same handle the row is keyed by.
        var redemptionId = CreatorRewardRedemptionId.From(_guids.NewSequentialGuid());
        var debitReference = $"creator-redemption:{redemptionId.Value:N}";

        var spent = await _points.TrySpendAsync(
            creator.Value.UserId,
            reward.PointsCost,
            $"Redemption:{reward.Key}",
            debitReference,
            cancellationToken);

        var failure = spent switch
        {
            RewardPointsSpendResult.InsufficientPoints => CreatorErrors.RewardInsufficientPoints,
            RewardPointsSpendResult.Blocked => CreatorErrors.RewardPointsUnavailable,
            _ => null
        };

        if (failure is not null)
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(failure);
        }

        var redemption = CreatorRewardRedemption.Open(
            redemptionId, reward, creator.Value.Id, creator.Value.UserId, debitReference, key, now);

        // What the platform issues for a reward is the reward's own business. Today every seeded
        // reward is an entitlement the grant reference names; a reward whose fulfilment needs another
        // module's act would be Pending here until that module answered.
        redemption.Fulfil($"{reward.GrantKind ?? "entitlement"}:{reward.Key}", now);

        _rewards.AddRedemption(redemption);

        // In a transaction: a violation here aborts it, so the guard rethrows rather than looking
        // up — the client's retry replays from the pre-check above.
        var commit = await CreatorIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => _rewards.FindByIdempotencyKeyAsync(creator.Value.Id, key!, token),
            cancellationToken,
            inTransaction: true);

        if (commit.IsFailure)
        {
            return Result.Failure<CreatorRewardRedemptionResponse>(commit.Error);
        }

        return Result.Success((commit.Value.Replayed ?? redemption).ToResponse());
    }
}
