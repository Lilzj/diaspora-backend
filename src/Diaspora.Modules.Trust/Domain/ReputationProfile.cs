using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// A member's reputation across the three dimensions the platform scores.
/// <para>
/// The counters below mirror the frontend's <c>UserReputation</c> field-for-field. That is
/// not laziness — the reputation screen renders each of them as its own line, and a server
/// that stored only the totals would force the client to keep its own tally and drift.
/// </para>
/// <para>
/// Scores are never assigned, only accumulated through <see cref="Apply"/>, which appends a
/// <see cref="ReputationEvent"/> for every change. The totals are therefore always a
/// replayable consequence of the event log rather than a number someone typed.
/// </para>
/// </summary>
public sealed class ReputationProfile : AggregateRoot<ReputationProfileId>, IAuditableEntity
{
    private readonly List<ReputationEvent> _events = [];

    public UserId UserId { get; private set; } = default!;

    // ── The three dimensions ─────────────────────────────────────────────

    /// <summary>Earned by creating and maintaining content.</summary>
    public int ContributionScore { get; private set; }

    /// <summary>Earned by proving who you are and where you live.</summary>
    public int VerificationScore { get; private set; }

    /// <summary>Earned from community feedback. The only one that can fall.</summary>
    public int TrustScore { get; private set; }

    public int CombinedScore => ContributionScore + VerificationScore + TrustScore;

    public ReputationLevel Level { get; private set; }

    // ── Contribution detail ──────────────────────────────────────────────

    public int GuidesPublished { get; private set; }
    public int GuidesApproved { get; private set; }
    public int GuidesUpdated { get; private set; }
    public int UpdatesPublished { get; private set; }
    public int UpdatesVerified { get; private set; }
    public int CommentsWritten { get; private set; }

    // ── Verification detail ──────────────────────────────────────────────

    public bool PhoneVerified { get; private set; }
    public bool EmailVerified { get; private set; }
    public bool GovernmentIdVerified { get; private set; }
    public bool ProfilePhotoAdded { get; private set; }
    public bool ProfileCompleted { get; private set; }
    public bool ProviderVerified { get; private set; }
    public int CountryVerificationsCount { get; private set; }

    // ── Trust detail ─────────────────────────────────────────────────────

    public int HelpfulVotesReceived { get; private set; }
    public int CommunityConfirmationsReceived { get; private set; }
    public int ViolationCount { get; private set; }
    public int ReportsResolved { get; private set; }

    /// <summary>
    /// Days since the last violation. Stored as the date it started rather than a running
    /// count, so it stays correct without a nightly job touching every row.
    /// </summary>
    public DateTimeOffset NoViolationSince { get; private set; }

    public DateTimeOffset LastCalculatedAt { get; private set; }

    public DateTimeOffset? LevelChangedAt { get; private set; }

    /// <summary>
    /// Optimistic concurrency token, advanced by every <see cref="Apply"/>.
    /// <para>
    /// Two awards for one member saved at once used to be last-writer-wins on the score columns:
    /// both events persisted, one score change was lost, and the profile stopped equalling the sum
    /// of its own history. Worse, a <c>MaxOccurrences = 1</c> rule read "not yet scored" in both,
    /// and both paid. With the token the second save fails, the outbox retries it, and the retry
    /// reads the first award's usage — so the cap holds and the arithmetic stays whole.
    /// </para>
    /// </summary>
    public int Revision { get; private set; }

    public IReadOnlyList<ReputationEvent> Events => _events.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private ReputationProfile() { }

    private ReputationProfile(ReputationProfileId id, UserId userId, DateTimeOffset at) : base(id)
    {
        UserId = userId;
        Level = ReputationLevel.Member;
        NoViolationSince = at;
        LastCalculatedAt = at;
    }

    public static ReputationProfile Open(UserId userId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return new ReputationProfile(ReputationProfileId.New(), userId, at);
    }

    /// <summary>
    /// Applies a scoring rule's award.
    /// <para>
    /// The single mutation point. Every counter below moves through here, so there is exactly
    /// one place where a score change can happen without an event beside it — and it does not
    /// exist.
    /// </para>
    /// </summary>
    public ReputationEvent Apply(
        ReputationAward award,
        string? sourceType,
        string? sourceId,
        string? countryCode,
        DateTimeOffset at,
        ReputationAwardKey? key = null)
    {
        ArgumentNullException.ThrowIfNull(award);

        var previousLevel = Level;
        var applied = MoveScore(award.Dimension, award.Points);

        ApplyCounter(award.ActionType, at, reverse: false);

        var recorded = ReputationEvent.Record(
            Id, award.ActionType, award.Dimension, award.Points,
            sourceType, sourceId, countryCode, award.Description, at, key, appliedPoints: applied,
            ruleSetVersion: award.RuleSetVersion);

        Record(recorded, award.Dimension, award.ActionType, award.Points, previousLevel, at);

        return recorded;
    }

    /// <summary>
    /// Takes an award back, or refunds a penalty, as a compensating event (D-9).
    /// <para>
    /// <b>Append-only, like every other change.</b> The original stays; the reversal names it in
    /// <see cref="ReputationEvent.ReversesEventId"/>, moves the score by the opposite of what the
    /// original actually applied, and moves the detail counter back. Through the same path as
    /// <see cref="Apply"/>, so the level and the domain events follow.
    /// </para>
    /// <para>
    /// <b>It records the original's opposite</b> as its points, and what it really moved in
    /// <see cref="ReputationEvent.AppliedPoints"/>, as <see cref="Apply"/> does. The breakdown sums
    /// recorded points, so an action and its reversal net to nothing there, and the floor stays in
    /// the adjustment where it was.
    /// </para>
    /// </summary>
    public ReputationEvent Reverse(
        ReputationEvent original, string reason, DateTimeOffset at, ReputationAwardKey key)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(key);

        if (original.ProfileId != Id)
        {
            throw new InvalidOperationException("That reputation event belongs to another profile.");
        }

        if (original.IsReversal)
        {
            throw new InvalidOperationException("A reversal is not itself reversed.");
        }

        var previousLevel = Level;
        var applied = MoveScore(original.Dimension, -(original.AppliedPoints ?? original.Points));

        ApplyCounter(original.ActionType, at, reverse: true);

        var recorded = ReputationEvent.Record(
            Id, original.ActionType, original.Dimension, -original.Points,
            original.SourceType, original.SourceId, original.CountryCode, reason, at, key,
            appliedPoints: applied, reversesEventId: original.Id,
            ruleSetVersion: original.RuleSetVersion);

        Record(recorded, original.Dimension, original.ActionType, -original.Points, previousLevel, at);

        return recorded;
    }

    /// <summary>Moves one dimension's score, floored at zero, and says by how much it really moved.</summary>
    private int MoveScore(ReputationDimension dimension, int points)
    {
        switch (dimension)
        {
            case ReputationDimension.contribution:
                var contribution = ContributionScore;
                ContributionScore = Math.Max(0, ContributionScore + points);
                return ContributionScore - contribution;

            case ReputationDimension.verification:
                var verification = VerificationScore;
                VerificationScore = Math.Max(0, VerificationScore + points);
                return VerificationScore - verification;

            case ReputationDimension.trust:
                var trust = TrustScore;
                TrustScore = Math.Max(0, TrustScore + points);
                return TrustScore - trust;

            default:
                return 0;
        }
    }

    private void Record(
        ReputationEvent recorded,
        ReputationDimension dimension,
        string actionType,
        int points,
        ReputationLevel previousLevel,
        DateTimeOffset at)
    {
        Level = ReputationThresholds.LevelFor(CombinedScore);
        LastCalculatedAt = at;
        Revision++;

        _events.Add(recorded);

        RaiseDomainEvent(new ReputationScoredDomainEvent(
            Id, UserId, dimension, actionType, points, at));

        if (Level != previousLevel)
        {
            LevelChangedAt = at;
            RaiseDomainEvent(new ReputationLevelChangedDomainEvent(
                Id, UserId, previousLevel, Level, CombinedScore, at));
        }
    }

    /// <summary>
    /// Moves the detail counter the frontend renders for this action, if there is one.
    /// <para>
    /// A switch rather than a dictionary of delegates because the set is small, closed, and
    /// far easier to read as a list of "this action moves that number".
    /// </para>
    /// </summary>
    /// <remarks>
    /// <paramref name="reverse"/> moves the counter back for a reversal: a count goes down (never
    /// below zero) and a flag goes false. A refunded violation leaves the no-violation streak where
    /// it is; the streak is a fact about time, not a score.
    /// </remarks>
    private void ApplyCounter(string actionType, DateTimeOffset at, bool reverse)
    {
        var step = reverse ? -1 : 1;
        var flag = !reverse;

        switch (actionType)
        {
            case ReputationActions.GuidePublished: GuidesPublished = Count(GuidesPublished, step); break;
            case ReputationActions.GuideApproved: GuidesApproved = Count(GuidesApproved, step); break;
            case ReputationActions.GuideUpdated: GuidesUpdated = Count(GuidesUpdated, step); break;
            case ReputationActions.UpdatePublished: UpdatesPublished = Count(UpdatesPublished, step); break;
            case ReputationActions.UpdateVerified: UpdatesVerified = Count(UpdatesVerified, step); break;
            case ReputationActions.CommentWritten: CommentsWritten = Count(CommentsWritten, step); break;
            case ReputationActions.HelpfulVoteReceived: HelpfulVotesReceived = Count(HelpfulVotesReceived, step); break;
            case ReputationActions.CommunityConfirmation:
                CommunityConfirmationsReceived = Count(CommunityConfirmationsReceived, step);
                break;
            case ReputationActions.ReportResolved: ReportsResolved = Count(ReportsResolved, step); break;

            case ReputationActions.PhoneVerified: PhoneVerified = flag; break;
            case ReputationActions.EmailVerified: EmailVerified = flag; break;
            case ReputationActions.GovernmentIdVerified: GovernmentIdVerified = flag; break;
            case ReputationActions.ProfilePhotoAdded: ProfilePhotoAdded = flag; break;
            case ReputationActions.ProfileCompleted: ProfileCompleted = flag; break;
            case ReputationActions.ProviderVerified: ProviderVerified = flag; break;
            case ReputationActions.CountryVerified:
                CountryVerificationsCount = Count(CountryVerificationsCount, step);
                break;

            case ReputationActions.ViolationRecorded:
                ViolationCount = Count(ViolationCount, step);

                if (!reverse)
                {
                    NoViolationSince = at; // the streak restarts
                }

                break;
        }
    }

    private static int Count(int value, int step) => Math.Max(0, value + step);

    /// <summary>Days without a violation, as at <paramref name="asOf"/>.</summary>
    public int NoViolationStreakDays(DateTimeOffset asOf) =>
        Math.Max(0, (int)(asOf - NoViolationSince).TotalDays);

    /// <summary>
    /// What the current level unlocks. Derived, never stored — a stored copy would go stale
    /// the moment the benefit table changed and quietly grant the wrong thing.
    /// </summary>
    public LevelBenefits Benefits => LevelBenefits.For(Level);

    /// <summary>Points still needed for the next rung, or null at the top.</summary>
    public (ReputationLevel Level, int MinScore, int PointsNeeded)? NextLevel
    {
        get
        {
            if (ReputationThresholds.NextAfter(Level) is not { } next)
            {
                return null;
            }

            var minimum = ReputationThresholds.MinimumFor(next);
            return (next, minimum, Math.Max(0, minimum - CombinedScore));
        }
    }

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
}

/// <summary>
/// What one level unlocks.
/// <para>
/// Mirrors the frontend's <c>LevelBenefits</c>. Note <c>canModerate</c> here is the
/// <em>reputation</em> half of the moderation question — the permission catalogue still has
/// to grant <c>trust:moderate</c>, and both must agree before anyone touches a queue. Earning
/// a level does not hand out a permission.
/// </para>
/// </summary>
public sealed record LevelBenefits
{
    public required bool CanPublishImmediately { get; init; }
    public required bool CanModerate { get; init; }
    public required bool IsFeaturedEligible { get; init; }

    /// <summary>
    /// <b>Gates nothing (REP-18).</b> Reward conversion is gated by identity verification alone, not
    /// by reputation. Kept because the Trust contract is frozen; the app does not show it.
    /// </summary>
    public required bool IsRewardsEligible { get; init; }
    public required bool HasBetaAccess { get; init; }
    public required bool HasExclusiveBadge { get; init; }

    /// <summary>Multiplier applied to this member's content in search ranking.</summary>
    public required decimal SearchBoostFactor { get; init; }

    public static LevelBenefits For(ReputationLevel level) => level switch
    {
        ReputationLevel.CommunityAmbassador => new LevelBenefits
        {
            CanPublishImmediately = true,
            CanModerate = true,
            IsFeaturedEligible = true,
            IsRewardsEligible = true,
            HasBetaAccess = true,
            HasExclusiveBadge = true,
            SearchBoostFactor = 2.0m
        },
        ReputationLevel.CommunityExpert => new LevelBenefits
        {
            CanPublishImmediately = true,
            CanModerate = true,
            IsFeaturedEligible = true,
            IsRewardsEligible = true,
            HasBetaAccess = true,
            HasExclusiveBadge = false,
            SearchBoostFactor = 1.5m
        },
        ReputationLevel.TrustedContributor => new LevelBenefits
        {
            CanPublishImmediately = true,
            CanModerate = false,
            IsFeaturedEligible = true,
            IsRewardsEligible = false,
            HasBetaAccess = false,
            HasExclusiveBadge = false,
            SearchBoostFactor = 1.25m
        },
        ReputationLevel.VerifiedResident => new LevelBenefits
        {
            CanPublishImmediately = false,
            CanModerate = false,
            IsFeaturedEligible = false,
            IsRewardsEligible = false,
            HasBetaAccess = false,
            HasExclusiveBadge = false,
            SearchBoostFactor = 1.1m
        },
        _ => new LevelBenefits
        {
            CanPublishImmediately = false,
            CanModerate = false,
            IsFeaturedEligible = false,
            IsRewardsEligible = false,
            HasBetaAccess = false,
            HasExclusiveBadge = false,
            SearchBoostFactor = 1.0m
        }
    };
}
