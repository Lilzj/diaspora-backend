using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Challenges;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Creator.Application.Challenges;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-004 — creator challenges.
//
// Five routes. Two things the register asks for that are easy to get subtly wrong, and both are
// handled in one place each:
//
//   The `state` filter accepts groups as well as members — ChallengeStateResolver owns the mapping
//   and a unit test asserts the three groups partition the nine states.
//
//   Progress is the server's. Nothing here trusts a client's opinion of what it has done; the
//   counters come from the recognised-activity ledger, through the consumer.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Whether one creator may join one challenge, and why not.
/// <para>
/// Its own type rather than a private method on a handler, because the discovery list, the detail
/// read and the join command all need the same answer — and three copies of an eligibility rule is
/// three answers. Static because it holds nothing: the rule is a function of the challenge and the
/// creator, and a stateless service registered in the container would be an instance nobody needs.
/// </para>
/// </summary>
public static class ChallengeEligibility
{
    public static (bool IsEligible, string? Reason) Evaluate(
        CreatorChallenge challenge,
        CreatorProfile? creator,
        CreatorChallengeEnrollment? enrollment,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (enrollment is not null)
        {
            return (false, "You have already joined this challenge.");
        }

        if (!challenge.IsJoinable(now))
        {
            return (false, "This challenge is not open to join.");
        }

        if (creator is null)
        {
            return (true, null);
        }

        if (creator.IsSuspended || creator.IsRestricted)
        {
            return (false, "Your account cannot join challenges at the moment.");
        }

        if (creator.EarnedStatus < challenge.MinimumStatus)
        {
            return (false, $"Open to {Describe(challenge.MinimumStatus)} and above.");
        }

        if (challenge.Countries.Count > 0
            && !challenge.Countries.Any(country =>
                string.Equals(country.CountryCode, creator.CountryCode, StringComparison.Ordinal)))
        {
            return (false, "This challenge is not running in your country.");
        }

        return (true, null);
    }

    private static string Describe(CreatorStatus status) => status switch
    {
        CreatorStatus.Contributor => "contributors",
        CreatorStatus.EmergingCreator => "emerging creators",
        CreatorStatus.VerifiedCreator => "verified creators",
        CreatorStatus.EstablishedCreator => "established creators",
        CreatorStatus.TopCreator => "top creators",
        _ => "members"
    };
}

/// <summary>
/// Challenge discovery.
/// <para>
/// The <c>state</c> parameter takes a group (<c>active</c>, <c>open</c>, <c>finished</c>) or a
/// member name, so the client's three-tab screen makes one request rather than three — which is what
/// the register asks for.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record ListChallengesQuery : IQuery<IReadOnlyList<CreatorChallengeResponse>>
{
    public string? State { get; init; }

    public int? Take { get; init; }
}

public sealed class ListChallengesQueryHandler
    : IQueryHandler<ListChallengesQuery, IReadOnlyList<CreatorChallengeResponse>>
{
    private readonly CreatorContext _context;
    private readonly ICreatorChallengeRepository _challenges;
    private readonly ICreatorChallengeEnrollmentRepository _enrollments;
    private readonly IClock _clock;

    public ListChallengesQueryHandler(
        CreatorContext context,
        ICreatorChallengeRepository challenges,
        ICreatorChallengeEnrollmentRepository enrollments,
        IClock clock)
    {
        _context = context;
        _challenges = challenges;
        _enrollments = enrollments;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<CreatorChallengeResponse>>> Handle(
        ListChallengesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CreatorChallengeResponse>>(caller.Error);
        }

        var now = _clock.UtcNow;
        var take = CreatorLimits.PageSize(query.Take);

        var challenges = await _challenges.ListPublishedAsync(take * 2, cancellationToken);
        var creator = await _context.FindCreatorAsync(cancellationToken);

        var enrolments = creator is null
            ? []
            : (await _enrollments.ListForCreatorAsync(creator.Id, cancellationToken))
                .ToDictionary(enrollment => enrollment.ChallengeId);

        var wanted = ChallengeStateResolver.Parse(query.State);

        var response = challenges
            .Select(challenge =>
            {
                enrolments.TryGetValue(challenge.Id, out var enrollment);

                var (eligible, reason) = ChallengeEligibility.Evaluate(challenge, creator, enrollment, now);

                return challenge.ToResponse(enrollment, eligible, reason, now);
            })
            .Where(challenge =>
                wanted is null
                || wanted.Any(state => string.Equals(
                    state.ToString(), challenge.State, StringComparison.Ordinal)))
            .Take(take)
            .ToList();

        return Result.Success<IReadOnlyList<CreatorChallengeResponse>>(response);
    }
}

/// <summary>One challenge, with this creator's progress against it.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetChallengeQuery : IQuery<CreatorChallengeResponse>
{
    public required Guid ChallengeId { get; init; }
}

public sealed class GetChallengeQueryHandler
    : IQueryHandler<GetChallengeQuery, CreatorChallengeResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorChallengeRepository _challenges;
    private readonly ICreatorChallengeEnrollmentRepository _enrollments;
    private readonly IClock _clock;

    public GetChallengeQueryHandler(
        CreatorContext context,
        ICreatorChallengeRepository challenges,
        ICreatorChallengeEnrollmentRepository enrollments,
        IClock clock)
    {
        _context = context;
        _challenges = challenges;
        _enrollments = enrollments;
        _clock = clock;
    }

    public async Task<Result<CreatorChallengeResponse>> Handle(
        GetChallengeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var challenge = await _challenges.GetAsync(
            CreatorChallengeId.From(query.ChallengeId), cancellationToken);

        if (challenge is null || !challenge.IsPublished)
        {
            return Result.Failure<CreatorChallengeResponse>(CreatorErrors.ChallengeNotFound);
        }

        var creator = await _context.FindCreatorAsync(cancellationToken);

        var enrollment = creator is null
            ? null
            : await _enrollments.FindAsync(challenge.Id, creator.Id, cancellationToken);

        var now = _clock.UtcNow;
        var (eligible, reason) = ChallengeEligibility.Evaluate(challenge, creator, enrollment, now);

        return Result.Success(challenge.ToResponse(enrollment, eligible, reason, now));
    }
}

/// <summary>
/// Joins a challenge. Idempotent on the key, and on the (challenge, creator) index behind it.
/// <para>
/// <b>Two concurrent joins produce one enrolment.</b> The unique index on (challenge, creator) is
/// the barrier that decides it even when neither request carried a key — which is the case the
/// register's "duplicate join" test exercises.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.challenge.join")]
public sealed record JoinChallengeCommand : ICommand<CreatorChallengeResponse>
{
    public required Guid ChallengeId { get; init; }

    public ClaimRequest? Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class JoinChallengeCommandValidator : AbstractValidator<JoinChallengeCommand>
{
    public JoinChallengeCommandValidator() => RuleFor(command => command.ChallengeId).NotEmpty();
}

public sealed class JoinChallengeCommandHandler
    : ICommandHandler<JoinChallengeCommand, CreatorChallengeResponse>
{
    private const string Operation = "creator-challenge-join";

    private readonly CreatorContext _context;
    private readonly ICreatorChallengeRepository _challenges;
    private readonly ICreatorChallengeEnrollmentRepository _enrollments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public JoinChallengeCommandHandler(
        CreatorContext context,
        ICreatorChallengeRepository challenges,
        ICreatorChallengeEnrollmentRepository enrollments,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _context = context;
        _challenges = challenges;
        _enrollments = enrollments;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<CreatorChallengeResponse>> Handle(
        JoinChallengeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorChallengeResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorChallengeResponse>(allowed.Error);
        }

        var challenge = await _challenges.GetAsync(
            CreatorChallengeId.From(command.ChallengeId), cancellationToken);

        if (challenge is null || !challenge.IsPublished)
        {
            return Result.Failure<CreatorChallengeResponse>(CreatorErrors.ChallengeNotFound);
        }

        var now = _clock.UtcNow;

        var key = CreatorIdempotency.Normalise(
            Operation, command.IdempotencyKeyHeader, command.Request?.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _enrollments.FindByJoinKeyAsync(key, cancellationToken);

            if (replayed is not null)
            {
                return Result.Success(challenge.ToResponse(replayed, false, null, now));
            }
        }

        var existing = await _enrollments.FindAsync(challenge.Id, creator.Value.Id, cancellationToken);

        if (existing is not null)
        {
            // Already joined. The answer rather than a conflict — a double-tap should not look like
            // an error, and the register's own "duplicate join" case expects the same enrolment back.
            return Result.Success(challenge.ToResponse(existing, false, null, now));
        }

        var (eligible, reason) = ChallengeEligibility.Evaluate(challenge, creator.Value, null, now);

        if (!eligible)
        {
            return Result.Failure<CreatorChallengeResponse>(
                challenge.IsJoinable(now)
                    ? CreatorErrors.ChallengeNotEligible
                    : CreatorErrors.ChallengeNotOpen);
        }

        var enrollment = CreatorChallengeEnrollment.Join(
            CreatorChallengeEnrollmentId.From(_guids.NewSequentialGuid()),
            challenge,
            creator.Value.Id,
            creator.Value.UserId,
            key,
            now);

        _enrollments.Add(enrollment);

        challenge.RecordJoin();

        // The lookup resolves the *natural* key as well as the idempotency one, because a join
        // collides on (challenge, creator) whether or not the caller sent a key — a double-tap with
        // no key is an ordinary event and must read back as the first request's enrolment rather
        // than as a 500.
        var commit = await CreatorIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            async token =>
                (key is null ? null : await _enrollments.FindByJoinKeyAsync(key, token))
                ?? await _enrollments.FindAsync(challenge.Id, creator.Value.Id, token),
            cancellationToken);

        if (commit.IsFailure)
        {
            return Result.Failure<CreatorChallengeResponse>(commit.Error);
        }

        var rendered = commit.Value.Replayed ?? enrollment;

        return Result.Success(challenge.ToResponse(rendered, false, null, now));
    }
}

/// <summary>Claims a completed challenge. The same three barriers a milestone claim carries.</summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.challenge.claim")]
public sealed record ClaimChallengeCommand : ICommand<CreatorChallengeResponse>
{
    public required Guid ChallengeId { get; init; }

    public ClaimRequest? Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class ClaimChallengeCommandValidator : AbstractValidator<ClaimChallengeCommand>
{
    public ClaimChallengeCommandValidator() => RuleFor(command => command.ChallengeId).NotEmpty();
}

public sealed class ClaimChallengeCommandHandler
    : ICommandHandler<ClaimChallengeCommand, CreatorChallengeResponse>
{
    private const string Operation = "creator-challenge-claim";

    private readonly CreatorContext _context;
    private readonly ICreatorChallengeRepository _challenges;
    private readonly ICreatorChallengeEnrollmentRepository _enrollments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ClaimChallengeCommandHandler(
        CreatorContext context,
        ICreatorChallengeRepository challenges,
        ICreatorChallengeEnrollmentRepository enrollments,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _challenges = challenges;
        _enrollments = enrollments;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CreatorChallengeResponse>> Handle(
        ClaimChallengeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorChallengeResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorChallengeResponse>(allowed.Error);
        }

        var challenge = await _challenges.GetAsync(
            CreatorChallengeId.From(command.ChallengeId), cancellationToken);

        if (challenge is null)
        {
            return Result.Failure<CreatorChallengeResponse>(CreatorErrors.ChallengeNotFound);
        }

        var now = _clock.UtcNow;

        var key = CreatorIdempotency.Normalise(
            Operation, command.IdempotencyKeyHeader, command.Request?.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _enrollments.FindByClaimKeyAsync(key, cancellationToken);

            if (replayed is not null)
            {
                return Result.Success(challenge.ToResponse(replayed, false, null, now));
            }
        }

        var enrollment = await _enrollments.FindAsync(
            challenge.Id, creator.Value.Id, cancellationToken);

        if (enrollment is null)
        {
            return Result.Failure<CreatorChallengeResponse>(CreatorErrors.ChallengeNotJoined);
        }

        // Recomputed against the challenge as it is now, so a re-seeded target is applied.
        enrollment.Recompute(challenge, now);

        var reference = $"challenge:{challenge.Key}:{creator.Value.Id.Value:N}";

        var claimed = enrollment.Claim(challenge, reference, key, now);

        if (claimed.IsFailure)
        {
            return Result.Failure<CreatorChallengeResponse>(claimed.Error);
        }

        _enrollments.Update(enrollment);

        var commit = await CreatorIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => _enrollments.FindByClaimKeyAsync(key!, token),
            cancellationToken);

        if (commit.IsFailure)
        {
            return Result.Failure<CreatorChallengeResponse>(commit.Error);
        }

        var rendered = commit.Value.Replayed ?? enrollment;

        return Result.Success(challenge.ToResponse(rendered, false, null, now));
    }
}

/// <summary>
/// The per-challenge leaderboard.
/// <para>
/// <b>The caller's own row is included even when it falls off the page</b>, computed as a count of
/// the enrolments ahead of it rather than by paging until it appears — the same "where am I" the
/// register asks for on the creator boards.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetChallengeLeaderboardQuery : IQuery<CreatorLeaderboardResponse>
{
    public required Guid ChallengeId { get; init; }

    public int? Take { get; init; }
}

public sealed class GetChallengeLeaderboardQueryHandler
    : IQueryHandler<GetChallengeLeaderboardQuery, CreatorLeaderboardResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorChallengeRepository _challenges;
    private readonly ICreatorChallengeEnrollmentRepository _enrollments;
    private readonly ICreatorProfileRepository _creators;
    private readonly IClock _clock;

    public GetChallengeLeaderboardQueryHandler(
        CreatorContext context,
        ICreatorChallengeRepository challenges,
        ICreatorChallengeEnrollmentRepository enrollments,
        ICreatorProfileRepository creators,
        IClock clock)
    {
        _context = context;
        _challenges = challenges;
        _enrollments = enrollments;
        _creators = creators;
        _clock = clock;
    }

    public async Task<Result<CreatorLeaderboardResponse>> Handle(
        GetChallengeLeaderboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var challenge = await _challenges.GetAsync(
            CreatorChallengeId.From(query.ChallengeId), cancellationToken);

        if (challenge is null || !challenge.IsPublished)
        {
            return Result.Failure<CreatorLeaderboardResponse>(CreatorErrors.ChallengeNotFound);
        }

        var take = CreatorLimits.LeaderboardSize(query.Take);

        var rows = await _enrollments.ListLeaderboardAsync(challenge.Id, take, cancellationToken);

        var creators = (await _creators.GetManyAsync(
                rows.Select(row => row.CreatorId).ToList(), cancellationToken))
            .ToDictionary(creator => creator.Id);

        // A creator who opted out of public ranking is absent from the board rather than shown. The
        // rank numbers below are positions in the served list, so an absence does not leave a hole
        // that discloses somebody was there.
        var entries = rows
            .Where(row => creators.TryGetValue(row.CreatorId, out var creator)
                && creator.IsDiscoverable && !creator.IsSuspended && !creator.IsRestricted)
            .Select((row, index) => Entry(creators[row.CreatorId], row.Score, index + 1))
            .ToList();

        var creator = await _context.FindCreatorAsync(cancellationToken);

        CreatorLeaderboardEntryResponse? viewer = null;
        var optedOut = false;

        if (creator is not null)
        {
            var mine = await _enrollments.FindAsync(challenge.Id, creator.Id, cancellationToken);

            if (mine is not null)
            {
                optedOut = !creator.IsDiscoverable;

                var rank = await _enrollments.RankOfAsync(
                    challenge.Id, mine.Score, cancellationToken);

                viewer = Entry(creator, mine.Score, rank);
            }
        }

        return Result.Success(new CreatorLeaderboardResponse
        {
            Scope = "challenge",
            Title = challenge.Title,
            Period = "challenge",
            PeriodLabel = "This challenge",
            ScoreLabel = "Points",
            ScoreDefinition =
                "The weighted total of every task action recorded for this challenge. A task counted "
                + "more times than it asked for still adds to the score.",
            Entries = entries,
            ViewerEntry = viewer,
            ViewerIsOptedOut = optedOut,
            UpdatedAt = _clock.UtcNow,
            RefreshNote = "Computed when you asked for it."
        });
    }

    private static CreatorLeaderboardEntryResponse Entry(
        CreatorProfile creator, long score, int rank) =>
        new()
        {
            Rank = rank,
            CreatorId = creator.Id.Value,
            DisplayName = creator.DisplayName,
            AvatarUrl = creator.AvatarUrl,
            Score = score,
            IsVerifiedCreator = creator.IsVerifiedCreator,
            CountryCode = creator.CountryCode
        };
}

/// <summary>
/// Records a recognised action against every challenge that counts it.
/// <para>
/// Extracted for the reason <c>MilestoneProgressService</c> is: DSP1001 forbids a handler reaching
/// another handler, and the consumer and the recompute both need this.
/// </para>
/// </summary>
public sealed class ChallengeProgressService
{
    private readonly ICreatorChallengeRepository _challenges;
    private readonly ICreatorChallengeEnrollmentRepository _enrollments;
    private readonly IClock _clock;

    public ChallengeProgressService(
        ICreatorChallengeRepository challenges,
        ICreatorChallengeEnrollmentRepository enrollments,
        IClock clock)
    {
        _challenges = challenges;
        _enrollments = enrollments;
        _clock = clock;
    }

    public async Task RecordAsync(
        CreatorProfile creator,
        string actionType,
        long occurrences,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creator);

        var now = _clock.UtcNow;

        var challenges = await _challenges.ListForActionAsync(actionType, now, cancellationToken);

        foreach (var challenge in challenges)
        {
            // Only somebody who joined makes progress. A challenge does not enrol people who happen
            // to have done the thing — joining is the member's own act and the window starts there.
            var enrollment = await _enrollments.FindAsync(
                challenge.Id, creator.Id, cancellationToken);

            if (enrollment is null)
            {
                continue;
            }

            enrollment.RecordActivity(challenge, actionType, occurrences, occurredAt, now);

            _enrollments.Update(enrollment);
        }
    }
}
