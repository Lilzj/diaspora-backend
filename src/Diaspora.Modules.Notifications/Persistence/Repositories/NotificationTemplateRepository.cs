using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Notifications.Persistence.Repositories;

/// <summary>
/// EF Core repository for the template catalogue.
/// <para>
/// <b><see cref="FindPublishedAsync"/> takes the highest published version, not the only one.</b>
/// Versioning is by supersession, so more than one published row under a key is possible for the
/// moment between a new version being published and the old one being retired — and serving the
/// older wording in that window would be a change nobody could explain.
/// </para>
/// </summary>
public sealed class NotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly DiasporaDbContext _dbContext;

    public NotificationTemplateRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<NotificationTemplate> Templates => _dbContext.Set<NotificationTemplate>();

    private IQueryable<NotificationTemplate> Full => Templates
        .Include(template => template.Localizations)
        .Include(template => template.Variants)
        .Include(template => template.Variables)
        .Include(template => template.Actions);

    public async Task<NotificationTemplate?> GetAsync(
        NotificationTemplateId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Full.FirstOrDefaultAsync(template => template.Id == id, cancellationToken);
    }

    public async Task<NotificationTemplate?> FindPublishedAsync(
        string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalized = key.Trim().ToLowerInvariant();

        return await Full
            .Where(template => template.Key == normalized)
            .Where(template => template.Status == TemplateStatus.Published)
            .OrderByDescending(template => template.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NotificationTemplate?> FindDraftAsync(
        string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalized = key.Trim().ToLowerInvariant();

        return await Full
            .Where(template => template.Key == normalized)
            .Where(template => template.Status == TemplateStatus.Draft)
            .OrderByDescending(template => template.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationTemplate>> ListVersionsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalized = key.Trim().ToLowerInvariant();

        return await Full
            .AsNoTracking()
            .Where(template => template.Key == normalized)
            .OrderByDescending(template => template.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> HighestVersionAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalized = key.Trim().ToLowerInvariant();

        var versions = await Templates
            .AsNoTracking()
            .Where(template => template.Key == normalized)
            .Select(template => template.Version)
            .ToListAsync(cancellationToken);

        return versions.Count == 0 ? 0 : versions.Max();
    }

    public async Task<CursorPagedResult<NotificationTemplate>> SearchAsync(
        TemplateQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Full.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.CategoryKey))
        {
            var category = filter.CategoryKey.Trim().ToLowerInvariant();
            query = query.Where(template => template.CategoryKey == category);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(template => template.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Language))
        {
            var language = filter.Language.Trim().ToLowerInvariant();
            query = query.Where(template =>
                template.Localizations.Any(localization => localization.Language == language));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(template =>
                EF.Functions.Like(template.Key, $"%{term}%")
                || (template.Description != null
                    && EF.Functions.Like(template.Description, $"%{term}%")));
        }

        var limit = Math.Clamp(
            filter.Limit ?? CursorPaginationRequest.DefaultLimit, 1, CursorPaginationRequest.MaxLimit);

        var cursor = NotificationCursor.Parse(filter.Cursor);
        var boundary = cursor?.AsTimestamp();

        if (boundary is { } after)
        {
            var exclusion = CursorBoundary.Exclusion(cursor!.LastKey, NotificationTemplateId.From);

            query = exclusion.IsExhausted
                ? query.Where(template => template.CreatedAt < after)
                : query.Where(template =>
                    template.CreatedAt < after
                    || (template.CreatedAt == after && !exclusion.Ids.Contains(template.Id)));
        }

        var page = await query
            .OrderByDescending(template => template.CreatedAt)
            .ThenByDescending(template => template.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > limit;
        var items = hasMore ? page.Take(limit).ToList() : page;

        if (items.Count == 0)
        {
            return CursorPagedResult<NotificationTemplate>.Empty();
        }

        var boundaryKey = CursorBoundary.Key(
            items,
            template => template.CreatedAt,
            template => template.Id.Value,
            cursor?.LastKey,
            boundary);

        var next = hasMore
            ? NotificationCursor.FromTimestamp(items[^1].CreatedAt, boundaryKey).Encode()
            : null;

        return CursorPagedResult<NotificationTemplate>.Create(items, next, hasMore);
    }

    public async Task AddAsync(
        NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        await Templates.AddAsync(template, cancellationToken);
    }

    public void Update(NotificationTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        NotificationAggregateUpdater.MarkDirty(_dbContext, template);
    }
}
