using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Feedback;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence.Repositories;

/// <summary>
/// EF Core repository for the feedback record.
/// <para>
/// <b>The suppression reads project to identifiers rather than materialising aggregates, and that is
/// the difference between a fast feed and a slow one.</b> Every generation asks what the member has
/// refused; the answer feeds a <c>HashSet</c>, and loading the aggregates would pull a member's whole
/// dismissal history to build a set of GUIDs and throw the rest away.
/// </para>
/// </summary>
public sealed class RecommendationFeedbackRepository : IRecommendationFeedbackRepository
{
    private readonly DiasporaDbContext _dbContext;

    public RecommendationFeedbackRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<RecommendationFeedback> Feedback => _dbContext.Set<RecommendationFeedback>();

    public void Add(RecommendationFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        Feedback.Add(feedback);
    }

    /// <summary>
    /// Targets a member's dismissals still suppress, for one type.
    /// <para>
    /// A null <c>SuppressedUntil</c> means forever, which is why the predicate is a disjunction
    /// rather than a comparison — and why writing it as <c>SuppressedUntil &gt; asOf</c> would
    /// silently stop honouring every permanent dismissal in the table.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ListSuppressedTargetIdsAsync(
        UserId userId,
        RecommendationTargetType targetType,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Feedback
            .Where(feedback => feedback.UserId == userId)
            .Where(feedback => feedback.Kind == RecommendationFeedbackKind.Dismissed)
            .Where(feedback => feedback.Target.TargetType == targetType)
            .Where(feedback => feedback.SuppressedUntil == null || feedback.SuppressedUntil > asOf)
            .Select(feedback => feedback.Target.TargetId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(RecommendationTargetType TargetType, Guid TargetId)>>
        ListAllSuppressedAsync(
            UserId userId, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var rows = await Feedback
            .Where(feedback => feedback.UserId == userId)
            .Where(feedback => feedback.Kind == RecommendationFeedbackKind.Dismissed)
            .Where(feedback => feedback.SuppressedUntil == null || feedback.SuppressedUntil > asOf)
            .Select(feedback => new
            {
                feedback.Target.TargetType,
                feedback.Target.TargetId
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows.Select(row => (row.TargetType, row.TargetId)).ToList();
    }

    public Task<bool> IsSuppressedAsync(
        UserId userId,
        RecommendationTargetType targetType,
        Guid targetId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return Feedback.AnyAsync(
            feedback => feedback.UserId == userId &&
                        feedback.Kind == RecommendationFeedbackKind.Dismissed &&
                        feedback.Target.TargetType == targetType &&
                        feedback.Target.TargetId == targetId &&
                        (feedback.SuppressedUntil == null || feedback.SuppressedUntil > asOf),
            cancellationToken);
    }

    public async Task<RecommendationFeedback?> FindLatestAsync(
        UserId userId,
        RecommendationTargetType targetType,
        Guid targetId,
        RecommendationFeedbackKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Feedback
            .Where(feedback => feedback.UserId == userId &&
                               feedback.Kind == kind &&
                               feedback.Target.TargetType == targetType &&
                               feedback.Target.TargetId == targetId)
            .OrderByDescending(feedback => feedback.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecommendationFeedback>> ListUnrolledAsync(
        int limit, CancellationToken cancellationToken = default) =>
        await Feedback
            .Where(feedback => feedback.RolledUpAt == null)
            .OrderBy(feedback => feedback.OccurredAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecommendationFeedback>> ListForUserAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Feedback
            .Where(feedback => feedback.UserId == userId)
            .OrderByDescending(feedback => feedback.OccurredAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public void Update(RecommendationFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        RecommendationAggregateUpdater.MarkDirty(_dbContext, feedback);
    }

    /// <summary>
    /// Retention, in bounded batches.
    /// <para>
    /// Through the change tracker rather than a set-based statement — B1.3's rule, which the platform
    /// owns the primitive for and a vertical does not reimplement. The batch bound is what makes
    /// materialising the rows affordable.
    /// </para>
    /// </summary>
    public async Task<int> DeleteOlderThanAsync(
        DateTimeOffset occurredBefore, int batchSize, CancellationToken cancellationToken = default)
    {
        var doomed = await Feedback
            .Where(feedback => feedback.OccurredAt < occurredBefore)
            .OrderBy(feedback => feedback.OccurredAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(cancellationToken);

        if (doomed.Count == 0)
        {
            return 0;
        }

        Feedback.RemoveRange(doomed);

        return doomed.Count;
    }

    /// <summary>
    /// Erasure. Unbounded on purpose — half-forgetting somebody is not an outcome worth batching for.
    /// </summary>
    public async Task<int> DeleteForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var doomed = await Feedback
            .Where(feedback => feedback.UserId == userId)
            .ToListAsync(cancellationToken);

        if (doomed.Count == 0)
        {
            return 0;
        }

        Feedback.RemoveRange(doomed);

        return doomed.Count;
    }
}
