using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Suggestions;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for improvement suggestions.
/// <para>
/// Pages on the timestamp alone — Community's precedent for aggregates without a slug — because
/// the primary key is a value-converted identifier EF cannot compare in SQL, and suggestions
/// have no other unique string to break ties with. Two suggestions submitted in the same tick
/// is rare enough that the simpler cursor wins.
/// </para>
/// </summary>
public sealed class GuideSuggestionRepository : IGuideSuggestionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public GuideSuggestionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<GuideSuggestion> Suggestions => _dbContext.Set<GuideSuggestion>();

    public async Task<GuideSuggestion?> GetAsync(
        GuideSuggestionId id, CancellationToken cancellationToken = default) =>
        await Suggestions.FirstOrDefaultAsync(
            suggestion => suggestion.Id == id, cancellationToken);

    public async Task<CursorPagedResult<GuideSuggestion>> ListForGuideAsync(
        GuideId guideId,
        GuideSuggestionStatus? status,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideId);

        var query = Suggestions
            .AsNoTracking()
            .Where(suggestion => suggestion.GuideId == guideId);

        if (status is { } wanted)
        {
            query = query.Where(suggestion => suggestion.Status == wanted);
        }

        var parsed = GuideCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } after)
        {
            // Not `CreatedAt < after` alone: that drops every suggestion sharing the boundary
            // instant, and suggestions arrive in bursts when a guide is shared. Excluding the
            // identifiers already returned is exact and, unlike comparing them, translates.
            // See CursorBoundary.
            var tie = CursorBoundary.Exclusion(parsed.LastKey, GuideSuggestionId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(suggestion =>
                suggestion.CreatedAt < after
                || (suggestion.CreatedAt == after && !exhausted && !seen.Contains(suggestion.Id)));
        }

        return await PageAsync(
            query.OrderByDescending(suggestion => suggestion.CreatedAt)
                .ThenByDescending(suggestion => suggestion.Id),
            limit,
            parsed,
            cancellationToken);
    }

    public async Task<CursorPagedResult<GuideSuggestion>> ListOpenAsync(
        string? cursor, int limit, CancellationToken cancellationToken = default)
    {
        var query = Suggestions
            .AsNoTracking()
            .Where(suggestion => suggestion.Status == GuideSuggestionStatus.Open);

        var parsed = GuideCursor.Parse(cursor);

        // Oldest first: the moderation queue works from the back, so nothing waits forever.
        if (parsed?.AsTimestamp() is { } after)
        {
            var tie = CursorBoundary.Exclusion(parsed.LastKey, GuideSuggestionId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(suggestion =>
                suggestion.CreatedAt > after
                || (suggestion.CreatedAt == after && !exhausted && !seen.Contains(suggestion.Id)));
        }

        return await PageAsync(
            query.OrderBy(suggestion => suggestion.CreatedAt)
                .ThenBy(suggestion => suggestion.Id),
            limit,
            parsed,
            cancellationToken);
    }

    public async Task AddAsync(
        GuideSuggestion suggestion, CancellationToken cancellationToken = default) =>
        await Suggestions.AddAsync(suggestion, cancellationToken);

    private static async Task<CursorPagedResult<GuideSuggestion>> PageAsync(
        IQueryable<GuideSuggestion> query,
        int limit,
        GuideCursor? previous,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit, 1, GuideLimits.MaxPageSize);

        var items = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = items.Count > effectiveLimit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore && items.Count > 0
            ? GuideCursor.FromTimestamp(
                items[^1].CreatedAt,
                CursorBoundary.Key(
                    items,
                    suggestion => suggestion.CreatedAt,
                    suggestion => suggestion.Id.Value,
                    previous?.LastKey,
                    previous?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<GuideSuggestion>.Create(items, next, hasMore);
    }
}
