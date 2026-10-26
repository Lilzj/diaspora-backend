using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Domain.Milestones;

/// <summary>
/// One creator's standing against one milestone.
/// <para>
/// <b>This row is the whole of CREATOR-GAP-003.</b> The register: "Nothing stores 'this member
/// completed First 10 Guides and has not claimed it'. The counters exist; the aggregate, the state
/// machine and the reward grant do not." All three are here.
/// </para>
/// <para>
/// <b>Completion is a stored fact, not a comparison done on a read.</b> A milestone that was
/// completed stays completed even if the underlying counter later falls — an unpublished guide does
/// not un-earn a badge somebody already has — and a milestone that was claimed can never be claimed
/// again, which is enforced by the state here and by a unique index on the claim key at the
/// database.
/// </para>
/// <para>
/// <b>The per-rule contributions are stored, not just the total.</b> A capped rule needs its own raw
/// count to apply the cap, and recomputing from the activity ledger on every read would make a
/// milestone list a hundred aggregate queries. These rows are the running tally, updated by the
/// consumer that recorded the activity.
/// </para>
/// </summary>
public sealed class CreatorMilestoneProgress
    : AggregateRoot<CreatorMilestoneProgressId>, IAuditableEntity
{
    private readonly List<MilestoneRuleProgress> _contributions = [];

    private CreatorMilestoneProgress(
        CreatorMilestoneProgressId id,
        CreatorId creatorId,
        Guid userId,
        CreatorMilestoneId milestoneId,
        string milestoneKey,
        DateTimeOffset startedAt) : base(id)
    {
        CreatorId = creatorId;
        UserId = userId;
        MilestoneId = milestoneId;
        MilestoneKey = milestoneKey;
        State = CreatorMilestoneState.Locked;
        CreatedAt = startedAt;
    }

    private CreatorMilestoneProgress() { }

    public CreatorId CreatorId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public CreatorMilestoneId MilestoneId { get; private set; } = default!;

    /// <summary>Denormalised so a claim's grant reference is readable without a join.</summary>
    public string MilestoneKey { get; private set; } = string.Empty;

    public CreatorMilestoneState State { get; private set; }

    /// <summary>The weighted, capped total. Never a raw count.</summary>
    public long Current { get; private set; }

    /// <summary>The target as it stood when this row last moved. Served with the progress.</summary>
    public long Target { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? ClaimedAt { get; private set; }

    public DateTimeOffset? ExpiredAt { get; private set; }

    /// <summary>
    /// The claim's idempotency key. Uniquely indexed, so two claims carrying one key produce one
    /// reward and the second replays the first's answer. §24 of the phase brief.
    /// </summary>
    public string? ClaimIdempotencyKey { get; private set; }

    /// <summary>
    /// The reward reference this claim published. Also this row's anti-double-award barrier at
    /// B15 — see <c>CreatorPointsEarnedDomainEvent</c>.
    /// </summary>
    public string? RewardGrantReference { get; private set; }

    public int RewardPointsGranted { get; private set; }

    public IReadOnlyList<MilestoneRuleProgress> Contributions => _contributions.AsReadOnly();

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorMilestoneProgress Begin(
        CreatorMilestoneProgressId id,
        CreatorId creatorId,
        Guid userId,
        CreatorMilestoneDefinition definition,
        DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var progress = new CreatorMilestoneProgress(
            id, creatorId, userId, definition.Id, definition.Key, startedAt)
        {
            Target = definition.Target
        };

        return progress;
    }

    /// <summary>
    /// Records one more occurrence of an action and recomputes the weighted, capped total.
    /// <para>
    /// <b>Returns true only on the transition into <see cref="CreatorMilestoneState.Completed"/>.</b>
    /// That is what makes <c>creator.milestone.completed</c> a once-per-milestone fact rather than a
    /// message per contribution, which §8 of the phase brief requires: "The event must be emitted
    /// only once according to the platform's event semantics."
    /// </para>
    /// </summary>
    public bool RecordActivity(
        CreatorMilestoneDefinition definition, string actionType, long occurrences, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (State is CreatorMilestoneState.Completed
            or CreatorMilestoneState.Claimed
            or CreatorMilestoneState.Expired)
        {
            // A completed milestone does not un-complete, and a claimed one is finished with.
            return false;
        }

        var rule = definition.Rules.FirstOrDefault(candidate =>
            string.Equals(candidate.ActionType, actionType, StringComparison.Ordinal));

        if (rule is null)
        {
            return false;
        }

        var row = _contributions.Find(contribution =>
            string.Equals(contribution.ActionType, actionType, StringComparison.Ordinal));

        if (row is null)
        {
            row = MilestoneRuleProgress.Create(actionType, 0);
            _contributions.Add(row);
        }

        row.Add(occurrences);

        return Recompute(definition, now);
    }

    /// <summary>
    /// Recomputes the total from the stored raw counts against the definition as it is now.
    /// <para>
    /// Separated from <see cref="RecordActivity"/> so that a re-seeded milestone whose weights or
    /// target changed is re-evaluated on the next read rather than staying on yesterday's rule for
    /// ever.
    /// </para>
    /// </summary>
    public bool Recompute(CreatorMilestoneDefinition definition, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (State is CreatorMilestoneState.Claimed or CreatorMilestoneState.Expired)
        {
            return false;
        }

        long total = 0;

        foreach (var rule in definition.Rules)
        {
            var raw = _contributions
                .Find(contribution =>
                    string.Equals(contribution.ActionType, rule.ActionType, StringComparison.Ordinal))
                ?.RawCount ?? 0;

            total += rule.Contribution(raw);
        }

        Current = total;
        Target = definition.Target;

        var progress = CreatorProgress.Of(Current, Target);
        var previous = State;

        if (definition.ExpiresAt is { } expiry && expiry <= now && !progress.IsComplete)
        {
            State = CreatorMilestoneState.Expired;
            ExpiredAt = now;
            Touch(now);
            return false;
        }

        State = progress switch
        {
            { IsComplete: true } => CreatorMilestoneState.Completed,
            { Percentage: var percentage } when percentage >= definition.AlmostCompletePercentage =>
                CreatorMilestoneState.AlmostComplete,
            { Current: > 0 } => CreatorMilestoneState.InProgress,
            _ => CreatorMilestoneState.Locked
        };

        var justCompleted = State == CreatorMilestoneState.Completed
            && previous != CreatorMilestoneState.Completed;

        if (justCompleted)
        {
            CompletedAt = now;

            RaiseDomainEvent(new CreatorMilestoneCompletedDomainEvent(
                Id, MilestoneId, CreatorId, UserId, MilestoneKey, now));
        }

        Touch(now);

        return justCompleted;
    }

    /// <summary>
    /// The member claims the reward.
    /// <para>
    /// <b>Refused unless the milestone is complete, and refused a second time however it is asked.</b>
    /// §8 of the phase brief: "A repeated claim must never generate another reward." Two barriers
    /// carry that: the state check here, and the unique index on
    /// <see cref="ClaimIdempotencyKey"/> which decides the race two concurrent claims would
    /// otherwise both win.
    /// </para>
    /// </summary>
    public Result Claim(
        CreatorMilestoneDefinition definition,
        string grantReference,
        string? idempotencyKey,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantReference);

        if (State == CreatorMilestoneState.Claimed)
        {
            return Result.Failure(CreatorErrors.MilestoneAlreadyClaimed);
        }

        if (State == CreatorMilestoneState.Expired)
        {
            return Result.Failure(CreatorErrors.MilestoneExpired);
        }

        if (State != CreatorMilestoneState.Completed)
        {
            return Result.Failure(CreatorErrors.MilestoneNotComplete);
        }

        State = CreatorMilestoneState.Claimed;
        ClaimedAt = now;
        ClaimIdempotencyKey = idempotencyKey;
        RewardGrantReference = grantReference;
        RewardPointsGranted = definition.RewardPoints;
        Touch(now);

        if (definition.RewardPoints > 0)
        {
            RaiseDomainEvent(new CreatorPointsEarnedDomainEvent(
                CreatorId,
                UserId,
                definition.RewardPoints,
                $"Milestone:{definition.Key}",
                grantReference,
                now,
                CreatorGrantPolicyVersions.Milestone(definition.Key, definition.PolicyRevision)));
        }

        return Result.Success();
    }

    /// <summary>The progress figure this row serves. Computed, so it can never disagree with itself.</summary>
    public CreatorProgress Progress(string? unit = null) => CreatorProgress.Of(Current, Target, unit);

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

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}

/// <summary>
/// How many times one action has happened toward one milestone.
/// <para>
/// The raw count, un-weighted and un-capped, because the weight and the cap are the definition's
/// and the definition can change. Storing the weighted figure would freeze a rule into a member's
/// row.
/// </para>
/// </summary>
public sealed class MilestoneRuleProgress
{
    private MilestoneRuleProgress(string actionType, long rawCount)
    {
        ActionType = actionType;
        RawCount = rawCount;
    }

    private MilestoneRuleProgress() { }

    public string ActionType { get; private set; } = string.Empty;

    public long RawCount { get; private set; }

    public static MilestoneRuleProgress Create(string actionType, long rawCount) =>
        new(actionType.Trim(), Math.Max(0, rawCount));

    internal void Add(long occurrences) => RawCount += Math.Max(0, occurrences);
}
