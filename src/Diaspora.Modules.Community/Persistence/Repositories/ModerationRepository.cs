using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for decisions and automated reviews.</summary>
public sealed class ModerationRepository : IModerationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ModerationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ModerationDecision> Decisions => _dbContext.Set<ModerationDecision>();

    private DbSet<ContentReview> Reviews => _dbContext.Set<ContentReview>();

    public async Task<IReadOnlyList<ModerationDecision>> ListDecisionsAsync(
        ContentReference content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Decisions
            .Where(decision =>
                decision.Content.Type == content.Type && decision.Content.Id == content.Id)
            .OrderByDescending(decision => decision.DecidedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CursorPagedResult<ModerationDecision>> ListRecentDecisionsAsync(
        FeedCursor? cursor, int limit, CancellationToken cancellationToken = default)
    {
        var query = Decisions.AsQueryable();

        if (cursor is not null)
        {
            // A moderator clearing a queue produces decisions in bursts that share a timestamp;
            // a plain `DecidedAt < after` would drop all but the first from the audit listing.
            // See CursorBoundary.
            var (start, end) = FeedBoundary.Window(cursor);
            var tie = CursorBoundary.Exclusion(cursor.BoundaryIds, ModerationDecisionId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(decision =>
                decision.DecidedAt < start
                || (decision.DecidedAt >= start
                    && decision.DecidedAt < end
                    && !exhausted && !seen.Contains(decision.Id)));
        }

        var items = await query
            .OrderByDescending(decision => decision.DecidedAt)
            .ThenByDescending(decision => decision.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore && items.Count > 0
            ? FeedCursor.FromTimestamp(
                items[^1].DecidedAt,
                items[^1].Id.Value,
                FeedBoundary.Key(
                    items, decision => decision.DecidedAt, decision => decision.Id.Value, cursor))
                .Encode()
            : null;

        return CursorPagedResult<ModerationDecision>.Create(items, next, hasMore);
    }

    public async Task<ContentReview?> GetLatestReviewAsync(
        ContentReference content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Reviews
            .Where(review =>
                review.Content.Type == content.Type && review.Content.Id == content.Id)
            .OrderByDescending(review => review.ReviewedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ContentReview>> GetLatestReviewsAsync(
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (contents.Count == 0)
        {
            return new Dictionary<string, ContentReview>(StringComparer.Ordinal);
        }

        var ids = contents.Select(content => content.Id).Distinct().ToList();

        var rows = await Reviews
            .Where(review => ids.Contains(review.Content.Id))
            .OrderByDescending(review => review.ReviewedAt)
            .ToListAsync(cancellationToken);

        // Grouped in memory rather than with a window function, because a queue page is at most
        // fifty items and a provider-specific SQL construct would not survive the SQLite the
        // integration suite runs on.
        return rows
            .GroupBy(review => review.Content.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public async Task AddDecisionAsync(
        ModerationDecision decision, CancellationToken cancellationToken = default) =>
        await Decisions.AddAsync(decision, cancellationToken);

    public async Task AddReviewAsync(
        ContentReview review, CancellationToken cancellationToken = default) =>
        await Reviews.AddAsync(review, cancellationToken);
}
