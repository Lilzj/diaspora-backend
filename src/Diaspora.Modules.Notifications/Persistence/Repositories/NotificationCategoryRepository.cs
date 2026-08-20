using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Notifications.Persistence.Repositories;

/// <summary>EF Core repository for the category catalogue.</summary>
public sealed class NotificationCategoryRepository : INotificationCategoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public NotificationCategoryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<NotificationCategory> Categories => _dbContext.Set<NotificationCategory>();

    public async Task<NotificationCategory?> GetAsync(
        NotificationCategoryId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Categories.FirstOrDefaultAsync(
            category => category.Id == id, cancellationToken);
    }

    public async Task<NotificationCategory?> FindByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalized = key.Trim().ToLowerInvariant();

        return await Categories.FirstOrDefaultAsync(
            category => category.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationCategory>> ListActiveAsync(
        CancellationToken cancellationToken = default) =>
        await Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.Position)
            .ThenBy(category => category.Key)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NotificationCategory>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Categories
            .OrderBy(category => category.Position)
            .ThenBy(category => category.Key)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        NotificationCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        await Categories.AddAsync(category, cancellationToken);
    }

    public void Update(NotificationCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        NotificationAggregateUpdater.MarkDirty(_dbContext, category);
    }
}
