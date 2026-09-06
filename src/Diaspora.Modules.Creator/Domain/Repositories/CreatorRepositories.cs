using Diaspora.Modules.Creator.Domain.Academy;
using Diaspora.Modules.Creator.Domain.Activity;
using Diaspora.Modules.Creator.Domain.Challenges;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Earnings;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Grants;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Milestones;
using Diaspora.Modules.Creator.Domain.Referrals;
using Diaspora.Modules.Creator.Domain.Rewards;
using Diaspora.Modules.Creator.Domain.Verification;

namespace Diaspora.Modules.Creator.Domain.Repositories;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Retrieval and staging, one interface per aggregate.
//
// Every method that returns a collection is either bounded by a page or bounded by an aggregate it
// belongs to. IReadRepository's own documentation states the rule the platform enforces: "no filter
// and no paging is an unbounded table scan as a public API."
//
// A lookup by idempotency key appears on every aggregate a keyed write creates, because that is
// B15's primitive and this module copies it rather than inventing a key store.
// ════════════════════════════════════════════════════════════════════════════════════════════════

public interface ICreatorProfileRepository
{
    void Add(CreatorProfile creator);

    void Update(CreatorProfile creator);

    Task<CreatorProfile?> GetAsync(CreatorId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A member's creator record, staged or saved.
    /// <para>
    /// Consults the change tracker first, for the reason B15's reward repository does: a consumer
    /// that opens a record and immediately writes against it does both in one unit of work, and a
    /// lookup that only asked the database would open a second one for the unique index to refuse.
    /// </para>
    /// </summary>
    Task<CreatorProfile?> FindByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Several creators by identifier, for a leaderboard's or a directory's hydration.</summary>
    Task<IReadOnlyList<CreatorProfile>> GetManyAsync(
        IReadOnlyCollection<CreatorId> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// The public directory. Every visibility rule is applied inside this query — see
    /// <c>CreatorDirectoryQuery</c> and the register's warning about client-side privacy filters.
    /// </summary>
    Task<CreatorDirectoryPage> SearchAsync(
        CreatorDirectoryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Facet counts over the same predicate the search applies. Never a wider one.</summary>
    Task<CreatorDirectoryFacets> FacetsAsync(
        CreatorDirectoryFilter filter, CancellationToken cancellationToken = default);
}

public interface ICreatorVerificationRepository
{
    void Add(CreatorVerificationApplication application);

    void Update(CreatorVerificationApplication application);

    Task<CreatorVerificationApplication?> GetAsync(
        CreatorVerificationApplicationId id, CancellationToken cancellationToken = default);

    Task<CreatorVerificationApplication?> FindForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default);

    Task<CreatorVerificationApplication?> FindByIdempotencyKeyAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>Approvals that have run out. The expiry sweep's page.</summary>
    Task<IReadOnlyList<CreatorVerificationApplication>> ListLapsedAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);
}

public interface ICreatorMilestoneRepository
{
    void Add(CreatorMilestoneDefinition definition);

    Task<IReadOnlyList<CreatorMilestoneDefinition>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    Task<CreatorMilestoneDefinition?> GetAsync(
        CreatorMilestoneId id, CancellationToken cancellationToken = default);

    Task<CreatorMilestoneDefinition?> FindByKeyAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>Definitions naming an action type, for the consumer that records activity.</summary>
    Task<IReadOnlyList<CreatorMilestoneDefinition>> ListForActionAsync(
        string actionType, CancellationToken cancellationToken = default);
}

public interface ICreatorMilestoneProgressRepository
{
    void Add(CreatorMilestoneProgress progress);

    void Update(CreatorMilestoneProgress progress);

    Task<CreatorMilestoneProgress?> GetAsync(
        CreatorMilestoneProgressId id, CancellationToken cancellationToken = default);

    Task<CreatorMilestoneProgress?> FindAsync(
        CreatorId creatorId, CreatorMilestoneId milestoneId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatorMilestoneProgress>> ListForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default);

    Task<CreatorMilestoneProgress?> FindByClaimKeyAsync(
        string key, CancellationToken cancellationToken = default);
}

public interface ICreatorChallengeRepository
{
    void Add(CreatorChallenge challenge);

    Task<CreatorChallenge?> GetAsync(
        CreatorChallengeId id, CancellationToken cancellationToken = default);

    Task<CreatorChallenge?> FindByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatorChallenge>> ListPublishedAsync(
        int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatorChallenge>> ListForActionAsync(
        string actionType, DateTimeOffset asOf, CancellationToken cancellationToken = default);
}

public interface ICreatorChallengeEnrollmentRepository
{
    void Add(CreatorChallengeEnrollment enrollment);

    void Update(CreatorChallengeEnrollment enrollment);

    Task<CreatorChallengeEnrollment?> GetAsync(
        CreatorChallengeEnrollmentId id, CancellationToken cancellationToken = default);

    Task<CreatorChallengeEnrollment?> FindAsync(
        CreatorChallengeId challengeId, CreatorId creatorId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatorChallengeEnrollment>> ListForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatorChallengeEnrollment>> ListForChallengeAsync(
        CreatorChallengeId challengeId, CancellationToken cancellationToken = default);

    /// <summary>The challenge's ranking, ordered and paged inside the query.</summary>
    Task<IReadOnlyList<CreatorChallengeEnrollment>> ListLeaderboardAsync(
        CreatorChallengeId challengeId, int take, CancellationToken cancellationToken = default);

    /// <summary>Where one enrolment sits in that ranking, without paging to find it.</summary>
    Task<int> RankOfAsync(
        CreatorChallengeId challengeId, long score, CancellationToken cancellationToken = default);

    Task<CreatorChallengeEnrollment?> FindByJoinKeyAsync(
        string key, CancellationToken cancellationToken = default);

    Task<CreatorChallengeEnrollment?> FindByClaimKeyAsync(
        string key, CancellationToken cancellationToken = default);
}

public interface ICommunityGrantRepository
{
    void Add(CommunityGrant grant);

    Task<CommunityGrant?> GetAsync(CommunityGrantId id, CancellationToken cancellationToken = default);

    Task<CommunityGrant?> FindByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityGrant>> ListAsync(
        string? category,
        string? countryCode,
        int take,
        CancellationToken cancellationToken = default);
}

public interface IGrantApplicationRepository
{
    void Add(GrantApplication application);

    void Update(GrantApplication application);

    Task<GrantApplication?> GetAsync(
        GrantApplicationId id, CancellationToken cancellationToken = default);

    Task<GrantApplication?> FindForCreatorAsync(
        CommunityGrantId grantId, CreatorId creatorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GrantApplication>> ListForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GrantApplication>> ListForReviewAsync(
        CommunityGrantId? grantId,
        GrantApplicationState? state,
        int take,
        CancellationToken cancellationToken = default);

    Task<GrantApplication?> FindBySubmitKeyAsync(
        string key, CancellationToken cancellationToken = default);
}

public interface IAcademyRepository
{
    void AddSection(AcademySection section);

    void AddCourse(AcademyCourse course);

    void AddLesson(AcademyLesson lesson);

    void AddQuiz(AcademyQuiz quiz);

    void UpdateCourse(AcademyCourse course);

    Task<IReadOnlyList<AcademySection>> ListSectionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AcademyCourse>> ListCoursesAsync(
        string? sectionKey, CourseLevel? level, CancellationToken cancellationToken = default);

    Task<AcademyCourse?> GetCourseAsync(
        AcademyCourseId id, CancellationToken cancellationToken = default);

    Task<AcademyCourse?> FindCourseBySlugAsync(
        string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AcademyLesson>> ListLessonsAsync(
        AcademyCourseId courseId, CancellationToken cancellationToken = default);

    Task<AcademyLesson?> GetLessonAsync(
        AcademyLessonId id, CancellationToken cancellationToken = default);

    Task<AcademyLesson?> FindLessonByKeyAsync(
        AcademyCourseId courseId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// The quiz, with its answer key. The only repository method that loads it, and its callers are
    /// the grader and the seeder.
    /// </summary>
    Task<AcademyQuiz?> GetQuizAsync(AcademyQuizId id, CancellationToken cancellationToken = default);

    Task<AcademyQuiz?> FindQuizByKeyAsync(string key, CancellationToken cancellationToken = default);
}

public interface ICreatorLearningProgressRepository
{
    void Add(CreatorLearningProgress progress);

    void Update(CreatorLearningProgress progress);

    Task<CreatorLearningProgress?> FindAsync(
        CreatorId creatorId, AcademyCourseId courseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatorLearningProgress>> ListForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default);
}

public interface ICreatorCertificateRepository
{
    void Add(CreatorCertificate certificate);

    Task<IReadOnlyList<CreatorCertificate>> ListForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default);

    Task<CreatorCertificate?> FindForCourseAsync(
        CreatorId creatorId, AcademyCourseId courseId, CancellationToken cancellationToken = default);
}

public interface ICreatorReferralRepository
{
    void AddCode(CreatorReferralCode code);

    void AddReferral(CreatorReferral referral);

    void UpdateReferral(CreatorReferral referral);

    Task<CreatorReferralCode?> FindCodeForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default);

    Task<CreatorReferralCode?> FindCodeAsync(
        string code, CancellationToken cancellationToken = default);

    Task<CreatorReferral?> GetAsync(CreatorReferralId id, CancellationToken cancellationToken = default);

    Task<CreatorReferral?> FindByInviteeAsync(
        Guid inviteeUserId, CancellationToken cancellationToken = default);

    Task<ReferralCounts> CountsForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default);

    Task<ReferralPage> ListForCreatorAsync(
        CreatorId creatorId,
        string? cursor,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatorReferral>> ListLapsedAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);
}

public interface ISignupBonusRepository
{
    void Add(SignupBonus bonus);

    void Update(SignupBonus bonus);

    Task<SignupBonus?> FindForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<SignupBonus?> FindByAcknowledgeKeyAsync(
        string key, CancellationToken cancellationToken = default);
}

public interface ICreatorRewardRepository
{
    void AddRule(CreatorPointRule rule);

    void AddReward(CreatorRewardDefinition reward);

    void AddRedemption(CreatorRewardRedemption redemption);

    void UpdateRedemption(CreatorRewardRedemption redemption);

    Task<IReadOnlyList<CreatorPointRule>> ListRulesAsync(CancellationToken cancellationToken = default);

    Task<CreatorPointRule?> FindRuleByKeyAsync(
        string key, CancellationToken cancellationToken = default);

    Task<CreatorPointRule?> FindRuleForActionAsync(
        string actionType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatorRewardDefinition>> ListRewardsAsync(
        CancellationToken cancellationToken = default);

    Task<CreatorRewardDefinition?> GetRewardAsync(
        CreatorRewardId id, CancellationToken cancellationToken = default);

    Task<CreatorRewardDefinition?> FindRewardByKeyAsync(
        string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatorRewardRedemption>> ListRedemptionsAsync(
        CreatorId creatorId, int take, CancellationToken cancellationToken = default);

    /// <summary>How many times this creator has redeemed one reward. The per-creator cap's input.</summary>
    Task<int> CountRedemptionsAsync(
        CreatorId creatorId, CreatorRewardId rewardId, CancellationToken cancellationToken = default);

    /// <summary>When this creator last redeemed one reward. The cooldown's input.</summary>
    Task<DateTimeOffset?> LastRedeemedAtAsync(
        CreatorId creatorId, CreatorRewardId rewardId, CancellationToken cancellationToken = default);

    Task<CreatorRewardRedemption?> FindByIdempotencyKeyAsync(
        string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// One creator's recorded earnings.
/// <para>
/// Reads only, plus the idempotency check. Nothing here aggregates in memory: a revenue summary over
/// a lifetime of earnings is a database sum, and loading every row to add them up is how a dashboard
/// becomes slower every month it is used.
/// </para>
/// </summary>
public interface ICreatorEarningRepository
{
    void Add(CreatorEarning earning);

    /// <summary>Whether this producing event has already been recorded. The idempotency barrier.</summary>
    Task<bool> ExistsAsync(string sourceReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// One creator's earnings, newest first, optionally narrowed.
    /// <para>
    /// <paramref name="after"/> is a position in the ledger rather than a timestamp, because two
    /// earnings can share an instant and a timestamp cursor would drop one of them. See
    /// <see cref="CreatorEarningCursor"/>.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<CreatorEarning>> ListForCreatorAsync(
        CreatorId creatorId,
        CreatorEarningState? state,
        CreatorEarningSource? source,
        CreatorEarningCursor? after,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Totals per state, summed at the database.
    /// <para>
    /// Keyed by state and currency together: a creator paid in two currencies has two totals, and
    /// adding them would produce a number that is not money in any currency.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<(CreatorEarningState State, string Currency, long MinorUnits, int Count)>>
        SumByStateAsync(
            CreatorId creatorId, DateTimeOffset? since, CancellationToken cancellationToken = default);

    /// <summary>Totals per source, summed at the database. Same currency rule as above.</summary>
    Task<IReadOnlyList<(CreatorEarningSource Source, string Currency, long MinorUnits, int Count)>>
        SumBySourceAsync(
            CreatorId creatorId, DateTimeOffset? since, CancellationToken cancellationToken = default);

    /// <summary>Earnings that have cleared their waiting period and are still pending.</summary>
    Task<IReadOnlyList<CreatorEarning>> ListClearableAsync(
        DateTimeOffset clearedBefore, int take, CancellationToken cancellationToken = default);
}

public interface ICreatorActivityRepository
{
    void Add(CreatorActivityEntry entry);

    /// <summary>Whether this producing event has already been counted for this creator.</summary>
    Task<bool> ExistsAsync(
        CreatorId creatorId, string sourceReference, CancellationToken cancellationToken = default);

    /// <summary>How many recognised actions this creator has, optionally since a moment.</summary>
    Task<long> CountForCreatorAsync(
        CreatorId creatorId, DateTimeOffset? since, CancellationToken cancellationToken = default);

    /// <summary>
    /// One creator's own entries, newest first.
    /// <para>
    /// <b>The read this interface was missing.</b> It could count entries, count one action, rank a
    /// leaderboard and answer whether a source event had been seen — everything except hand back
    /// the rows. So the platform recorded a creator's activity faithfully and had no way to show it
    /// to them, and the workspace carried *"Diaspora cannot show your activity history yet"* over a
    /// table that was being written to on every publish.
    /// </para>
    /// <para>
    /// Cursor-paged on <c>OccurredAt</c> rather than offset-paged: an active creator's history grows
    /// while they are reading it, and an offset would show them the same entry twice.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<CreatorActivityEntry>> ListForCreatorAsync(
        CreatorId creatorId,
        DateTimeOffset? before,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>How many times one action has happened for one creator since a moment.</summary>
    Task<long> CountActionAsync(
        CreatorId creatorId,
        string actionType,
        DateTimeOffset? since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The leaderboard, computed in the database.
    /// <para>
    /// Grouped, summed and ordered by the provider rather than in memory, because a leaderboard
    /// that materialised every entry to rank twenty creators would fall over on the first busy
    /// month.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<LeaderboardRow>> LeaderboardAsync(
        LeaderboardQuery query, CancellationToken cancellationToken = default);

    /// <summary>One creator's own row, whether or not it is on the page.</summary>
    Task<LeaderboardRow?> ViewerRowAsync(
        LeaderboardQuery query, CreatorId creatorId, CancellationToken cancellationToken = default);
}

// ── Query shapes ────────────────────────────────────────────────────────────────────────────────

/// <summary>Everything the public creator directory filters on.</summary>
public sealed record CreatorDirectoryFilter
{
    public string? Query { get; init; }

    public string? CountryCode { get; init; }

    public string? Category { get; init; }

    public string? Expertise { get; init; }

    public string? Language { get; init; }

    /// <summary>Only creators who hold the creator-programme verification.</summary>
    public bool? Verified { get; init; }

    public string? Cursor { get; init; }

    public int Take { get; init; } = 20;
}

public sealed record CreatorDirectoryPage(
    IReadOnlyList<CreatorProfile> Creators, string? NextCursor, bool HasMore);

/// <summary>
/// Facet counts over the visible set.
/// <para>
/// <b>Computed under the same visibility predicate the search applies.</b> The register: "A private
/// creator must not affect public facet counts in a way that reveals their existence."
/// </para>
/// </summary>
public sealed record CreatorDirectoryFacets(
    IReadOnlyList<FacetCount> Countries,
    IReadOnlyList<FacetCount> Categories,
    IReadOnlyList<FacetCount> Expertise,
    IReadOnlyList<FacetCount> Languages,
    int VerifiedCount,
    int TotalCount);

public sealed record FacetCount(string Value, int Count);

/// <summary>The four counters the referral dashboard shows, kept distinct.</summary>
public sealed record ReferralCounts(
    int Invited, int SignedUp, int Qualified, int Rewarded, int PendingRewards, long PointsEarned);

public sealed record ReferralPage(
    IReadOnlyList<CreatorReferral> Referrals, string? NextCursor, bool HasMore);

/// <summary>What a leaderboard is being asked for.</summary>
public sealed record LeaderboardQuery
{
    public required LeaderboardScope Scope { get; init; }

    public required LeaderboardPeriod Period { get; init; }

    public string? CountryCode { get; init; }

    public Guid? CommunityId { get; init; }

    public int Take { get; init; } = 20;

    /// <summary>The moment the ranking is computed for. Passed in so a test can pin it.</summary>
    public required DateTimeOffset AsOf { get; init; }
}

/// <summary>One row of a ranking, before the creator is hydrated onto it.</summary>
public sealed record LeaderboardRow(CreatorId CreatorId, long Score, int Rank);
