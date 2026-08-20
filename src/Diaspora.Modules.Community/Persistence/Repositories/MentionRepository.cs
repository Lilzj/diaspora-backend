using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Mentions;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for mention records.</summary>
public sealed class MentionRepository : IMentionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public MentionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Mention> Mentions => _dbContext.Set<Mention>();

    public async Task<CursorPagedResult<Mention>> ListForTargetAsync(
        MentionTargetType targetType,
        Guid targetId,
        FeedCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Mentions.Where(mention =>
            mention.TargetType == targetType && mention.TargetId == targetId);

        if (cursor is not null)
        {
            // Every mention in one post is written in a single unit of work and therefore shares
            // one timestamp exactly. A plain `MentionedAt < after` would drop all but the first.
            // See CursorBoundary.
            var (start, end) = FeedBoundary.Window(cursor);
            var tie = CursorBoundary.Exclusion(cursor.BoundaryIds, MentionId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(mention =>
                mention.MentionedAt < start
                || (mention.MentionedAt >= start
                    && mention.MentionedAt < end
                    && !exhausted && !seen.Contains(mention.Id)));
        }

        var items = await query
            .OrderByDescending(mention => mention.MentionedAt)
            .ThenByDescending(mention => mention.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore && items.Count > 0
            ? FeedCursor.FromTimestamp(
                items[^1].MentionedAt,
                items[^1].Id.Value,
                FeedBoundary.Key(
                    items, mention => mention.MentionedAt, mention => mention.Id.Value, cursor))
                .Encode()
            : null;

        return CursorPagedResult<Mention>.Create(items, next, hasMore);
    }

    public async Task<IReadOnlyList<Mention>> ListForContentAsync(
        ContentReference content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Mentions
            .Where(mention =>
                mention.Content.Type == content.Type && mention.Content.Id == content.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Mention mention, CancellationToken cancellationToken = default) =>
        await Mentions.AddAsync(mention, cancellationToken);

    public void RemoveRange(IEnumerable<Mention> mentions) => Mentions.RemoveRange(mentions);
}
