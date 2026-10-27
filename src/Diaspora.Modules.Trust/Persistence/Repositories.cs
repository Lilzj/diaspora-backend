using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Trust.Persistence;

/// <summary>
/// Persistence contract for the <see cref="VerificationRequest"/> aggregate.
/// <para>
/// Query shapes are named rather than exposing <c>IQueryable</c>. A repository that hands
/// back a queryable lets a handler compose a query the module never reviewed and never
/// indexed for — which on a table holding every verification submission is how a page load
/// becomes a table scan.
/// </para>
/// </summary>
public interface IVerificationRequestRepository
{
    /// <summary>Loads a request with its documents and reviews, tracked for mutation.</summary>
    Task<VerificationRequest?> GetAsync(
        VerificationRequestId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The member's own open request of a given type, if any. Used to refuse duplicates —
    /// a member with two live diaspora submissions is two reviewers doing the same work.
    /// <para>
    /// Member-scoped only: a claim the member raised about a provider or organisation they own
    /// is a different claim and does not answer this question.
    /// </para>
    /// </summary>
    Task<VerificationRequest?> GetOpenByTypeAsync(
        UserId userId, VerificationType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// The open request about a particular subject aggregate, if any.
    /// <para>
    /// Keyed on the subject rather than the member because that is what identity means for a
    /// non-member claim: two co-owners submitting for the same business are making one claim,
    /// and one owner submitting for two businesses is making two.
    /// </para>
    /// </summary>
    Task<VerificationRequest?> GetOpenBySubjectAsync(
        VerificationType type,
        string subjectType,
        Guid? subjectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The request carrying a submitting module's correlation identifier, whatever its status.
    /// The idempotency lookup behind <c>ITrustVerificationSubmission</c>.
    /// </summary>
    Task<VerificationRequest?> GetByCorrelationAsync(
        Guid correlationId, CancellationToken cancellationToken = default);

    /// <summary>Every request the member currently holds a grant for.</summary>
    Task<IReadOnlyList<VerificationRequest>> ListGrantedAsync(
        UserId userId, DateTimeOffset asOf, CancellationToken cancellationToken = default);

    /// <summary>The member's own requests, newest first.</summary>
    Task<PagedResult<VerificationRequest>> ListForUserAsync(
        UserId userId,
        PaginationRequest pagination,
        VerificationType? type = null,
        VerificationStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>The staff review queue, oldest-due first.</summary>
    Task<PagedResult<VerificationRequest>> ListQueueAsync(
        PaginationRequest pagination,
        VerificationType? type = null,
        VerificationStatus? status = null,
        UserId? assignedTo = null,
        bool overdueOnly = false,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    /// <summary>Grants past their expiry, for the lapse sweep.</summary>
    Task<IReadOnlyList<VerificationRequest>> ListExpiredGrantsAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(VerificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Persistence contract for the <see cref="TrustProfile"/> aggregate.</summary>
public interface ITrustProfileRepository
{
    /// <summary>Loads a profile with badges and country standings, tracked.</summary>
    Task<TrustProfile?> GetByUserAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Read-only projection for the hot <c>ITrustService</c> path.</summary>
    Task<TrustProfile?> GetByUserReadOnlyAsync(
        UserId userId, CancellationToken cancellationToken = default);

    Task AddAsync(TrustProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Members whose last assessment is above the baseline, the stalest first: the risk sweep's work
    /// list (RWD-14, CS-4b). Oldest first, so a member reassessed last pass waits behind one who was
    /// not, and a batch never starves the rest.
    /// </summary>
    Task<IReadOnlyList<UserId>> ListAboveBaselineRiskAsync(
        int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// How often a scoring rule has already fired for one member.
/// <para>
/// <paramref name="InCountry"/> is zero when the caller asked about no country, so a rule that
/// is not country-scoped reads <paramref name="Total"/> and never sees the difference.
/// </para>
/// </summary>
/// <param name="Total">Every occurrence, whatever country it belonged to.</param>
/// <param name="InCountry">Occurrences within the country asked about.</param>
/// <param name="LastAt">When it last fired, for the cooldown.</param>
public readonly record struct RuleUsage(int Total, int InCountry, DateTimeOffset? LastAt)
{
    public static RuleUsage None => new(0, 0, null);
}

/// <summary>
/// Everything one action has paid a member on one dimension: how many times, and the points it
/// recorded. One row of a reputation breakdown, before it is named.
/// <para>
/// <paramref name="Points"/> is what the events recorded, not what the score moved by. A score is
/// held at zero, so a penalty larger than the score records its full amount and moves the score
/// by less; the difference is the breakdown's adjustment, not an error in this sum.
/// </para>
/// </summary>
public readonly record struct ReputationEventSummary(
    ReputationDimension Dimension, string ActionType, int Count, int Points);

/// <summary>Persistence contract for the <see cref="ReputationProfile"/> aggregate.</summary>
public interface IReputationProfileRepository
{
    Task<ReputationProfile?> GetByUserAsync(UserId userId, CancellationToken cancellationToken = default);

    Task<ReputationProfile?> GetByUserReadOnlyAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Scoring history, newest first.</summary>
    Task<PagedResult<ReputationEvent>> ListEventsAsync(
        UserId userId,
        PaginationRequest pagination,
        ReputationDimension? dimension = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many times a rule has already fired for this member, and when it last did.
    /// Backs the <c>MaxOccurrences</c> and <c>CooldownHours</c> caps without loading the
    /// whole event history.
    /// <para>
    /// <paramref name="countryCode"/> is supplied for a country-scoped rule, and the result
    /// then carries the count within that country as well as the total. A cap on a rule that
    /// belongs to a country means once <em>per country</em>: the alternative counts a member's
    /// Mauritius residency against their move to the UK, and pays them nothing for the second.
    /// </para>
    /// </summary>
    Task<RuleUsage> GetRuleUsageAsync(
        UserId userId,
        string actionType,
        string? countryCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this member has already been awarded for <paramref name="key"/> — including an
    /// award staged earlier in this same unit of work and not yet saved.
    /// </summary>
    Task<bool> HasAwardAsync(
        UserId userId, ReputationAwardKey key, CancellationToken cancellationToken = default);

    /// <summary>A profile by its own id, staged or saved. Reversal finds profiles by the events it undoes.</summary>
    Task<ReputationProfile?> GetByIdAsync(
        ReputationProfileId profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every award or penalty with this source that has not been reversed, across all profiles
    /// (D-9). How a removed comment's award is found when the removal names no author.
    /// </summary>
    /// <param name="sourceType">Null matches any source type.</param>
    Task<IReadOnlyList<ReputationEvent>> ListUnreversedBySourceAsync(
        string? sourceType, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>The unreversed events on one member's profile that carry any of these award keys.</summary>
    Task<IReadOnlyList<ReputationEvent>> ListUnreversedByAwardKeysAsync(
        UserId userId, IReadOnlyCollection<string> awardKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// The member's whole scoring history, totalled per dimension and action — one aggregate
    /// query, however long the history.
    /// <para>
    /// Takes the profile's id rather than the member's: the one caller already holds the profile,
    /// and a profile opened on the spot for a new member has no history to look up.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ReputationEventSummary>> SummariseEventsAsync(
        ReputationProfileId profileId, CancellationToken cancellationToken = default);

    Task AddAsync(ReputationProfile profile, CancellationToken cancellationToken = default);
}

/// <summary>Persistence contract for the <see cref="Appeal"/> aggregate.</summary>
public interface IAppealRepository
{
    Task<Appeal?> GetAsync(AppealId id, CancellationToken cancellationToken = default);

    /// <summary>An open appeal against the same target, if one exists.</summary>
    Task<Appeal?> GetOpenForTargetAsync(
        UserId userId, string targetId, CancellationToken cancellationToken = default);

    Task<PagedResult<Appeal>> ListForUserAsync(
        UserId userId,
        PaginationRequest pagination,
        AppealStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>The staff appeals queue.</summary>
    Task<PagedResult<Appeal>> ListQueueAsync(
        PaginationRequest pagination,
        AppealStatus? status = null,
        AppealType? type = null,
        UserId? assignedTo = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Appeal appeal, CancellationToken cancellationToken = default);
}

/// <summary>Persistence contract for the trust timeline.</summary>
public interface ITrustEventRepository
{
    Task<PagedResult<TrustEvent>> ListForUserAsync(
        UserId userId,
        PaginationRequest pagination,
        bool memberVisibleOnly = true,
        CancellationToken cancellationToken = default);

    Task AddAsync(TrustEvent trustEvent, CancellationToken cancellationToken = default);
}

/// <summary>Persistence contract for the moderation queue.</summary>
public interface IModerationItemRepository
{
    Task<ModerationItem?> GetAsync(ModerationItemId id, CancellationToken cancellationToken = default);

    /// <summary>An open item for the same content, so reporting twice does not queue twice.</summary>
    Task<ModerationItem?> GetOpenForContentAsync(
        ModerationContentType contentType, string contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The queue, ordered by priority then age — an urgent item that arrived a minute ago
    /// outranks a low-priority one from yesterday, which is the point of having priorities.
    /// </summary>
    Task<PagedResult<ModerationItem>> ListQueueAsync(
        PaginationRequest pagination,
        ModerationQueueStatus? status = null,
        ModerationContentType? contentType = null,
        ModerationPriority? minimumPriority = null,
        UserId? assignedTo = null,
        bool overdueOnly = false,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(ModerationItem item, CancellationToken cancellationToken = default);
}

/// <summary>Persistence contract for community confirmations.</summary>
public interface ICommunityContributionRepository
{
    /// <summary>
    /// Whether this member already confirmed this target. Backs the one-per-member
    /// invariant, which is the only thing making the signal worth counting.
    /// </summary>
    Task<bool> ExistsAsync(
        UserId confirmedBy, string targetType, string targetId,
        CancellationToken cancellationToken = default);

    Task<int> CountForTargetAsync(
        string targetType, string targetId, CancellationToken cancellationToken = default);

    Task AddAsync(CommunityContribution contribution, CancellationToken cancellationToken = default);
}

/// <summary>Persistence contract for the evidence counted toward a member's risk (RWD-14, CS-4b).</summary>
public interface IRiskSignalRepository
{
    /// <summary>Whether this evidence is already on record. Backs the one-row-per-source rule.</summary>
    Task<bool> ExistsAsync(string sourceKey, CancellationToken cancellationToken = default);

    /// <summary>The member's evidence since a moment, the window an assessment reads.</summary>
    Task<IReadOnlyList<RiskSignal>> ListSinceAsync(
        UserId userId, DateTimeOffset since, CancellationToken cancellationToken = default);

    Task AddAsync(RiskSignal signal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks up to <paramref name="limit"/> pieces of evidence older than <paramref name="cutoff"/>
    /// for removal, oldest first, and says how many. The caller's save removes them.
    /// </summary>
    Task<int> RemoveOlderThanAsync(
        DateTimeOffset cutoff, int limit, CancellationToken cancellationToken = default);
}
