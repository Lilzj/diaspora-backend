using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence.Repositories;

/// <summary>
/// EF Core repository for the candidate projection.
/// <para>
/// <b><see cref="GetOrProjectAsync"/> is an upsert because integration event delivery is
/// at-least-once and unordered.</b> The same publication event arrives twice; an update arrives
/// before the publication it updates. A projection that assumed creation-then-update would either
/// duplicate rows or drop the update, and both failures are invisible until somebody notices a
/// vertical is under-recommended.
/// </para>
/// <para>
/// <b><see cref="ListByTargetsAsync"/> is written as an <c>IN</c> over composed keys rather than a
/// disjunction of pairs.</b> A predicate built as <c>(type = a AND id = b) OR (type = c AND id = d)</c>
/// twenty times over is a query plan no planner handles well and a parameter count some providers
/// refuse. Filtering on the identifier set and re-matching the pair in memory is one indexed scan
/// and a cheap post-filter, and the over-fetch is bounded by how likely two verticals are to share a
/// GUID, which is not likely.
/// </para>
/// </summary>
public sealed class RecommendationCandidateRepository : IRecommendationCandidateRepository
{
    private readonly DiasporaDbContext _dbContext;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RecommendationCandidateRepository(
        DiasporaDbContext dbContext, IGuidGenerator guids, IClock clock)
    {
        _dbContext = dbContext;
        _guids = guids;
        _clock = clock;
    }

    private DbSet<RecommendationCandidate> Candidates => _dbContext.Set<RecommendationCandidate>();

    public async Task<RecommendationCandidate?> FindAsync(
        RecommendationTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default) =>
        await Candidates
            .Include(candidate => candidate.Attributes)
            .FirstOrDefaultAsync(
                candidate => candidate.Target.TargetType == targetType &&
                             candidate.Target.TargetId == targetId,
                cancellationToken);

    public async Task<RecommendationCandidate> GetOrProjectAsync(
        RecommendationTargetType targetType,
        Guid targetId,
        string sourceModule,
        string title,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceModule);

        // The change tracker first, for the reason every get-or-create in this platform does it: one
        // event can produce two projections for one target, and a second query would not see the
        // first.
        var tracked = _dbContext.ChangeTracker
            .Entries<RecommendationCandidate>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(candidate =>
                candidate.Target.TargetType == targetType && candidate.Target.TargetId == targetId);

        if (tracked is not null)
        {
            return tracked;
        }

        if (await FindAsync(targetType, targetId, cancellationToken) is { } existing)
        {
            return existing;
        }

        var created = RecommendationCandidate.Project(
            RecommendationCandidateId.From(_guids.NewSequentialGuid()),
            TargetReference.Create(targetType, targetId),
            sourceModule,
            title,
            _clock.UtcNow);

        await Candidates.AddAsync(created, cancellationToken);

        return created;
    }

    /// <summary>
    /// The engine's pool.
    /// <para>
    /// Ordered by popularity so that a pool smaller than the table is the most engaged part of it
    /// rather than an arbitrary part of it. This is retrieval, not ranking — see
    /// <see cref="CandidateQuery"/>.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<RecommendationCandidate>> SearchAsync(
        CandidateQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var candidates = Candidates
            .Include(candidate => candidate.Attributes)
            .Where(candidate => candidate.IsActive)
            .Where(candidate => candidate.ExpiresAt == null || candidate.ExpiresAt > query.AsOf);

        if (query.TargetTypes.Count > 0)
        {
            var types = query.TargetTypes.ToList();
            candidates = candidates.Where(candidate => types.Contains(candidate.Target.TargetType));
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            var country = query.CountryCode.Trim().ToUpperInvariant();

            // Content belonging to nowhere is included by default. A visa guide has no country, and
            // a country-filtered pool that excluded it would empty the guides surface for everybody.
            candidates = query.IncludeGlobal
                ? candidates.Where(candidate =>
                    candidate.CountryCode == country || candidate.CountryCode == null)
                : candidates.Where(candidate => candidate.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(query.LanguageCode))
        {
            var language = query.LanguageCode.Trim().ToLowerInvariant();

            candidates = candidates.Where(candidate =>
                candidate.LanguageCode == language || candidate.LanguageCode == null);
        }

        if (query.CategoryKeys.Count > 0)
        {
            var keys = query.CategoryKeys.Select(key => key.Trim().ToLowerInvariant()).ToList();
            candidates = candidates.Where(candidate =>
                candidate.CategoryKey != null && keys.Contains(candidate.CategoryKey));
        }

        if (query.PublishedAfter is { } publishedAfter)
        {
            candidates = candidates.Where(candidate => candidate.PublishedAt >= publishedAfter);
        }

        if (query.VerifiedOnly)
        {
            candidates = candidates.Where(candidate => candidate.IsVerified);
        }

        // The trust floor is applied here as well as in the policy. Not redundant: filtering in the
        // query means a member who can see nothing above their level does not pull four hundred rows
        // they will never be shown, which on a restricted account is the whole pool.
        candidates = candidates.Where(candidate =>
            candidate.MinimumTrustLevel <= query.MemberTrustLevel);

        if (!query.MemberIsVerified)
        {
            candidates = candidates.Where(candidate =>
                candidate.Visibility != CandidateVisibility.Verified);
        }

        if (query.ExcludedTargetIds.Count > 0)
        {
            var excluded = query.ExcludedTargetIds.ToList();
            candidates = candidates.Where(candidate => !excluded.Contains(candidate.Target.TargetId));
        }

        return await candidates
            .OrderByDescending(candidate => candidate.PopularityScore)
            .ThenByDescending(candidate => candidate.PublishedAt)
            .Take(Math.Max(1, query.Limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecommendationCandidate>> ListByTargetsAsync(
        IReadOnlyCollection<(RecommendationTargetType TargetType, Guid TargetId)> targets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);

        if (targets.Count == 0)
        {
            return [];
        }

        // One indexed scan on the identifiers, then re-match the pair in memory. See the class
        // summary for why the pair is not expressed as a disjunction.
        var ids = targets.Select(target => target.TargetId).Distinct().ToList();

        var loaded = await Candidates
            .Include(candidate => candidate.Attributes)
            .Where(candidate => ids.Contains(candidate.Target.TargetId))
            .ToListAsync(cancellationToken);

        var wanted = targets.ToHashSet();

        return loaded
            .Where(candidate => wanted.Contains((candidate.Target.TargetType, candidate.Target.TargetId)))
            .ToList();
    }

    public async Task<IReadOnlyList<RecommendationCandidate>> ListMostPopularAsync(
        IReadOnlyList<RecommendationTargetType> targetTypes,
        string? countryCode,
        int limit,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        var candidates = Candidates
            .Include(candidate => candidate.Attributes)
            .Where(candidate => candidate.IsActive)
            .Where(candidate => candidate.ExpiresAt == null || candidate.ExpiresAt > asOf);

        if (targetTypes.Count > 0)
        {
            var types = targetTypes.ToList();
            candidates = candidates.Where(candidate => types.Contains(candidate.Target.TargetType));
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();

            candidates = candidates.Where(candidate =>
                candidate.CountryCode == country || candidate.CountryCode == null);
        }

        return await candidates
            .OrderByDescending(candidate => candidate.PopularityScore)
            .ThenByDescending(candidate => candidate.EngagementCount)
            .ThenByDescending(candidate => candidate.PublishedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListOwnedTargetIdsAsync(
        UserId ownerUserId,
        RecommendationTargetType targetType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownerUserId);

        return await Candidates
            .Where(candidate => candidate.OwnerUserId == ownerUserId &&
                                candidate.Target.TargetType == targetType)
            .Select(candidate => candidate.Target.TargetId)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveAsync(
        RecommendationTargetType targetType, CancellationToken cancellationToken = default) =>
        Candidates.CountAsync(
            candidate => candidate.IsActive && candidate.Target.TargetType == targetType,
            cancellationToken);

    public void Update(RecommendationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        RecommendationAggregateUpdater.MarkDirty(_dbContext, candidate);
    }

    /// <summary>
    /// Retires candidates whose source withdrew them long enough ago.
    /// <para>
    /// <b>Removed through the change tracker, which for a soft-deletable aggregate means B1.0.1's
    /// interceptor turns it into a soft delete and repairs the owned graph on the way.</b> That is
    /// the intended outcome and worth being explicit about: a withdrawn candidate disappears from
    /// every pool the moment its source unpublishes, and this sweep is what stops the table growing
    /// without bound while keeping "was recommendable until Tuesday" answerable for the analytics.
    /// </para>
    /// <para>
    /// <c>IgnoreQueryFilters</c> is on the read for the reason the platform records elsewhere: the
    /// global soft-delete filter hides the rows a retention pass exists to find, so without it the
    /// sweep would find nothing and report success.
    /// </para>
    /// </summary>
    public async Task<int> DeleteWithdrawnAsync(
        DateTimeOffset withdrawnBefore, int batchSize, CancellationToken cancellationToken = default)
    {
        var doomed = await Candidates
            .IgnoreQueryFilters()
            .Include(candidate => candidate.Attributes)
            .Where(candidate => !candidate.IsActive)
            .Where(candidate => candidate.ModifiedAt != null && candidate.ModifiedAt < withdrawnBefore)
            .OrderBy(candidate => candidate.ModifiedAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(cancellationToken);

        if (doomed.Count == 0)
        {
            return 0;
        }

        Candidates.RemoveRange(doomed);

        return doomed.Count;
    }
}
