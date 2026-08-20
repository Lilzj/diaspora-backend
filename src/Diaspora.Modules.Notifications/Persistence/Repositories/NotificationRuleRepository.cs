using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Notifications.Persistence.Repositories;

/// <summary>
/// EF Core repository for the routing catalogue.
/// <para>
/// <see cref="ListForEventAsync"/> runs once per consumed integration event — which is to say, on
/// every meaningful thing that happens on the platform. It is a small table behind a covering index
/// and is read with no tracking, because the dispatcher only ever reads rules and a tracked read
/// would put every rule in the change tracker of every request that publishes anything.
/// </para>
/// </summary>
public sealed class NotificationRuleRepository : INotificationRuleRepository
{
    private readonly DiasporaDbContext _dbContext;

    public NotificationRuleRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<NotificationRule> Rules => _dbContext.Set<NotificationRule>();

    public async Task<NotificationRule?> FindByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalized = key.Trim().ToLowerInvariant();

        return await Rules.FirstOrDefaultAsync(rule => rule.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationRule>> ListForEventAsync(
        string eventName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var normalized = eventName.Trim();

        return await Rules
            .AsNoTracking()
            .Where(rule => rule.EventName == normalized && rule.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationRule>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Rules
            .OrderBy(rule => rule.EventName)
            .ThenBy(rule => rule.Audience)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(NotificationRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await Rules.AddAsync(rule, cancellationToken);
    }

    public void Update(NotificationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        NotificationAggregateUpdater.MarkDirty(_dbContext, rule);
    }
}
