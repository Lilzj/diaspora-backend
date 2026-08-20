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
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(award);

        var previousLevel = Level;

        switch (award.Dimension)
        {
            case ReputationDimension.contribution:
                ContributionScore = Math.Max(0, ContributionScore + award.Points);
                break;

            case ReputationDimension.verification:
                VerificationScore = Math.Max(0, VerificationScore + award.Points);
                break;

            case ReputationDimension.trust:
                TrustScore = Math.Max(0, TrustScore + award.Points);
                break;
        }

        ApplyCounter(award.ActionType, at);

        Level = ReputationThresholds.LevelFor(CombinedScore);
        LastCalculatedAt = at;

        var recorded = ReputationEvent.Record(
            Id, award.ActionType, award.Dimension, award.Points,
            sourceType, sourceId, countryCode, award.Description, at);

        _events.Add(recorded);

        RaiseDomainEvent(new ReputationScoredDomainEvent(
            Id, UserId, award.Dimension, award.ActionType, award.Points, at));

        if (Level != previousLevel)
        {
            LevelChangedAt = at;
            RaiseDomainEvent(new ReputationLevelChangedDomainEvent(
                Id, UserId, previousLevel, Level, CombinedScore, at));
        }

        return recorded;
    }

    /// <summary>
    /// Moves the detail counter the frontend renders for this action, if there is one.
    /// <para>
    /// A switch rather than a dictionary of delegates because the set is small, closed, and
    /// far easier to read as a list of "this action moves that number".
    /// </para>
    /// </summary>
    private void ApplyCounter(string actionType, DateTimeOffset at)
    {
        switch (actionType)
        {
            case ReputationActions.GuidePublished: GuidesPublished++; break;
            case ReputationActions.GuideApproved: GuidesApproved++; break;
            case ReputationActions.GuideUpdated: GuidesUpdated++; break;
            case ReputationActions.UpdatePublished: UpdatesPublished++; break;
            case ReputationActions.UpdateVerified: UpdatesVerified++; break;
            case ReputationActions.CommentWritten: CommentsWritten++; break;
            case ReputationActions.HelpfulVoteReceived: HelpfulVotesReceived++; break;
            case ReputationActions.CommunityConfirmation: CommunityConfirmationsReceived++; break;
            case ReputationActions.ReportResolved: ReportsResolved++; break;

            case ReputationActions.PhoneVerified: PhoneVerified = true; break;
            case ReputationActions.EmailVerified: EmailVerified = true; break;
            case ReputationActions.GovernmentIdVerified: GovernmentIdVerified = true; break;
            case ReputationActions.ProfilePhotoAdded: ProfilePhotoAdded = true; break;
            case ReputationActions.ProfileCompleted: ProfileCompleted = true; break;
            case ReputationActions.ProviderVerified: ProviderVerified = true; break;
            case ReputationActions.CountryVerified: CountryVerificationsCount++; break;

            case ReputationActions.ViolationRecorded:
                ViolationCount++;
                NoViolationSince = at; // the streak restarts
                break;
        }
    }

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
