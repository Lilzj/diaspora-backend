using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Domain.Challenges;

/// <summary>
/// One creator's participation in one challenge.
/// <para>
/// <b>The register's objection to doing this on a device is the reason this row exists:</b>
/// "Counting a member's own guides on the device to advance 'create 3 helpful guides' would produce
/// a number that is wrong on a second device and wrong the moment a guide is unpublished — and would
/// let the client decide when a reward is due."
/// </para>
/// <para>
/// <b>Per-task progress is stored, and the overall figure is derived from it.</b> A challenge with
/// three tasks is complete when all three are, not when the sum of a total crosses a number; storing
/// the per-task counts is what makes both the task list and the overall bar the same truth.
/// </para>
/// <para>
/// <b>Two idempotency keys, not one.</b> Joining and claiming are different operations with
/// different consequences, and a client that retried a join must not replay a claim. Each is
/// uniquely indexed on its own column.
/// </para>
/// </summary>
public sealed class CreatorChallengeEnrollment
    : AggregateRoot<CreatorChallengeEnrollmentId>, IAuditableEntity
{
    private readonly List<ChallengeTaskProgress> _tasks = [];

    private CreatorChallengeEnrollment(
        CreatorChallengeEnrollmentId id,
        CreatorChallengeId challengeId,
        CreatorId creatorId,
        Guid userId,
        DateTimeOffset joinedAt) : base(id)
    {
        ChallengeId = challengeId;
        CreatorId = creatorId;
        UserId = userId;
        JoinedAt = joinedAt;
        State = CreatorChallengeState.Joined;
        CreatedAt = joinedAt;
    }

    private CreatorChallengeEnrollment() { }

    public CreatorChallengeId ChallengeId { get; private set; } = default!;

    public CreatorId CreatorId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public CreatorChallengeState State { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? ClaimedAt { get; private set; }

    /// <summary>
    /// The leaderboard's score for this enrolment: the weighted sum across every task, uncapped by
    /// the task target so that somebody who did ten of a task that asked for three outranks
    /// somebody who did exactly three.
    /// </summary>
    public long Score { get; private set; }

    public string? JoinIdempotencyKey { get; private set; }

    public string? ClaimIdempotencyKey { get; private set; }

    public string? RewardGrantReference { get; private set; }

    public int RewardPointsGranted { get; private set; }

    public IReadOnlyList<ChallengeTaskProgress> Tasks => _tasks.AsReadOnly();

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorChallengeEnrollment Join(
        CreatorChallengeEnrollmentId id,
        CreatorChallenge challenge,
        CreatorId creatorId,
        Guid userId,
        string? idempotencyKey,
        DateTimeOffset joinedAt)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var enrollment = new CreatorChallengeEnrollment(
            id, challenge.Id, creatorId, userId, joinedAt)
        {
            JoinIdempotencyKey = idempotencyKey
        };

        foreach (var task in challenge.Tasks)
        {
            enrollment._tasks.Add(ChallengeTaskProgress.Begin(task.TaskId, task.Target));
        }

        return enrollment;
    }

    /// <summary>
    /// Counts one more occurrence of an action against every task that asks for it.
    /// <para>
    /// <b>Returns true only on the transition into <see cref="CreatorChallengeState.Completed"/>.</b>
    /// <c>challenge.progress.changed</c> is raised whenever a task moved, and
    /// <c>challenge.completed</c> exactly once — the two are separate events because a client
    /// invalidating a progress bar and a member being congratulated are different things.
    /// </para>
    /// </summary>
    public bool RecordActivity(
        CreatorChallenge challenge,
        string actionType,
        long occurrences,
        DateTimeOffset occurredAt,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (State is CreatorChallengeState.Completed
            or CreatorChallengeState.Claimed
            or CreatorChallengeState.Expired
            or CreatorChallengeState.Cancelled
            or CreatorChallengeState.Failed)
        {
            return false;
        }

        var moved = false;

        foreach (var task in challenge.Tasks.Where(candidate =>
            string.Equals(candidate.ActionType, actionType, StringComparison.Ordinal)))
        {
            if (!IsInsideWindow(task, challenge, occurredAt))
            {
                continue;
            }

            var row = _tasks.Find(progress => progress.TaskId == task.TaskId);

            if (row is null)
            {
                row = ChallengeTaskProgress.Begin(task.TaskId, task.Target);
                _tasks.Add(row);
            }

            row.Add(occurrences, task.Weight);
            moved = true;
        }

        return moved && Recompute(challenge, now);
    }

    /// <summary>
    /// Re-derives the overall state from the per-task rows.
    /// <para>
    /// Separated so a re-seeded challenge whose targets changed is re-evaluated rather than staying
    /// on yesterday's rule, and so a sweep can fail an enrolment whose challenge has ended.
    /// </para>
    /// </summary>
    public bool Recompute(CreatorChallenge challenge, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (State is CreatorChallengeState.Claimed or CreatorChallengeState.Cancelled)
        {
            return false;
        }

        Score = _tasks.Sum(task => task.WeightedCount);

        var previous = State;
        var complete = challenge.Tasks.Count > 0
            && challenge.Tasks.All(task =>
                _tasks.Find(progress => progress.TaskId == task.TaskId) is { } row
                && row.RawCount >= task.Target);

        if (complete)
        {
            State = CreatorChallengeState.Completed;

            if (previous != CreatorChallengeState.Completed)
            {
                CompletedAt = now;

                RaiseDomainEvent(new ChallengeCompletedDomainEvent(
                    Id, ChallengeId, CreatorId, UserId, now));
            }
        }
        else if (challenge.IsCancelled)
        {
            State = CreatorChallengeState.Cancelled;
        }
        else if (now > challenge.EndsAt)
        {
            // Ended without finishing. Failed rather than Expired: the challenge expired, the
            // attempt did not succeed, and a member reads the second more honestly than the first.
            State = CreatorChallengeState.Failed;
        }
        else
        {
            State = _tasks.Exists(task => task.RawCount > 0)
                ? CreatorChallengeState.InProgress
                : CreatorChallengeState.Joined;
        }

        var overall = OverallProgress(challenge);

        RaiseDomainEvent(new ChallengeProgressChangedDomainEvent(
            Id, ChallengeId, CreatorId, UserId,
            overall.Current, overall.Target, overall.Percentage, now));

        Touch(now);

        return complete && previous != CreatorChallengeState.Completed;
    }

    /// <summary>
    /// The member claims the reward.
    /// <para>
    /// Refused unless every task is done, and refused a second time however it is asked — the same
    /// two barriers a milestone claim carries, and for the same reason.
    /// </para>
    /// </summary>
    public Result Claim(
        CreatorChallenge challenge, string grantReference, string? idempotencyKey, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantReference);

        if (State == CreatorChallengeState.Claimed)
        {
            return Result.Failure(CreatorErrors.ChallengeAlreadyClaimed);
        }

        if (State != CreatorChallengeState.Completed)
        {
            return Result.Failure(CreatorErrors.ChallengeNotComplete);
        }

        State = CreatorChallengeState.Claimed;
        ClaimedAt = now;
        ClaimIdempotencyKey = idempotencyKey;
        RewardGrantReference = grantReference;
        RewardPointsGranted = challenge.RewardPoints;
        Touch(now);

        if (challenge.RewardPoints > 0)
        {
            RaiseDomainEvent(new CreatorPointsEarnedDomainEvent(
                CreatorId,
                UserId,
                challenge.RewardPoints,
                $"Challenge:{challenge.Key}",
                grantReference,
                now,
                CreatorGrantPolicyVersions.Challenge(challenge.Key, challenge.PolicyRevision)));
        }

        return Result.Success();
    }

    /// <summary>
    /// The overall figure across every task.
    /// <para>
    /// <b>Each task contributes what it asked for and no more</b>, so a member who did ten of a task
    /// that wanted three does not appear 300% complete. The leaderboard's <see cref="Score"/> is the
    /// uncapped figure and is a different number on purpose.
    /// </para>
    /// </summary>
    public CreatorProgress OverallProgress(CreatorChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        long current = 0;
        long target = 0;

        foreach (var task in challenge.Tasks)
        {
            var row = _tasks.Find(progress => progress.TaskId == task.TaskId);

            target += task.Target;
            current += Math.Min(row?.RawCount ?? 0, task.Target);
        }

        return CreatorProgress.Of(current, target);
    }

    public ChallengeTaskProgress? FindTask(CreatorChallengeTaskId taskId) =>
        _tasks.Find(task => task.TaskId == taskId);

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private bool IsInsideWindow(
        ChallengeTask task, CreatorChallenge challenge, DateTimeOffset occurredAt) =>
        task.Window switch
        {
            ChallengeTaskWindow.SinceJoined => occurredAt >= JoinedAt,
            ChallengeTaskWindow.ChallengeWindow =>
                occurredAt >= challenge.StartsAt && occurredAt <= challenge.EndsAt,
            _ => true
        };

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}

/// <summary>
/// How far one task has got.
/// <para>
/// <see cref="RawCount"/> decides completion against the task's target;
/// <see cref="WeightedCount"/> feeds the leaderboard. Two numbers because a challenge can weight a
/// featured guide above an ordinary one for ranking and still ask for "three guides" to finish.
/// </para>
/// </summary>
public sealed class ChallengeTaskProgress
{
    private ChallengeTaskProgress(CreatorChallengeTaskId taskId, long target)
    {
        TaskId = taskId;
        Target = target;
    }

    private ChallengeTaskProgress() { }

    public CreatorChallengeTaskId TaskId { get; private set; } = default!;

    public long RawCount { get; private set; }

    public long WeightedCount { get; private set; }

    /// <summary>The target as it stood when this row last moved. Served with the task.</summary>
    public long Target { get; private set; }

    public static ChallengeTaskProgress Begin(CreatorChallengeTaskId taskId, long target) =>
        new(taskId, target);

    internal void Add(long occurrences, int weight)
    {
        var safe = Math.Max(0, occurrences);

        RawCount += safe;
        WeightedCount += safe * Math.Max(1, weight);
    }
}
