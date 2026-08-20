using Diaspora.Modules.Notifications.Domain.Analytics;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Notifications.Persistence.Repositories;

/// <summary>
/// EF Core repository for the daily rollup.
/// <para>
/// <b><see cref="GetOrOpenAsync"/> checks the change tracker first, and that is correctness rather
/// than speed.</b> One unit of work moves several counters on the same bucket — created, then
/// queued, then queued again for a second channel — and a second database lookup would find nothing
/// (the first is added, not saved) and add a duplicate the unique index rejects at save time,
/// failing the whole dispatch.
/// </para>
/// </summary>
public sealed class NotificationAnalyticsRepository : INotificationAnalyticsRepository
{
    /// <summary>
    /// Rows returned before aggregation when a caller states no bound.
    /// <para>
    /// A year of one category on one channel is a few hundred rows; a year of everything is tens of
    /// thousands. The cap keeps an unbounded question from becoming an unbounded query, and the
    /// analytics handler says when it truncated.
    /// </para>
    /// </summary>
    private const int DefaultQueryLimit = 5000;

    private readonly DiasporaDbContext _dbContext;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public NotificationAnalyticsRepository(
        DiasporaDbContext dbContext, IGuidGenerator guids, IClock clock)
    {
        _dbContext = dbContext;
        _guids = guids;
        _clock = clock;
    }

    private DbSet<NotificationAnalytics> Analytics => _dbContext.Set<NotificationAnalytics>();

    public async Task<NotificationAnalytics> GetOrOpenAsync(
        DateOnly day,
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);

        var category = categoryKey.Trim().ToLowerInvariant();
        var template = templateKey.Trim().ToLowerInvariant();

        var tracked = _dbContext.ChangeTracker
            .Entries<NotificationAnalytics>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(bucket =>
                bucket.Day == day
                && bucket.Channel == channel
                && string.Equals(bucket.CategoryKey, category, StringComparison.Ordinal)
                && string.Equals(bucket.TemplateKey, template, StringComparison.Ordinal));

        if (tracked is not null)
        {
            return tracked;
        }

        var existing = await Analytics.FirstOrDefaultAsync(
            bucket => bucket.Day == day
                && bucket.CategoryKey == category
                && bucket.Channel == channel
                && bucket.TemplateKey == template,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var opened = NotificationAnalytics.OpenBucket(
            NotificationAnalyticsId.From(_guids.NewSequentialGuid()),
            day,
            category,
            channel,
            template,
            _clock.UtcNow);

        await Analytics.AddAsync(opened, cancellationToken);

        return opened;
    }

    public async Task<IReadOnlyList<NotificationAnalytics>> QueryAsync(
        NotificationAnalyticsFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Analytics.AsNoTracking();

        if (filter.From is { } from)
        {
            query = query.Where(bucket => bucket.Day >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(bucket => bucket.Day <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.CategoryKey))
        {
            var category = filter.CategoryKey.Trim().ToLowerInvariant();
            query = query.Where(bucket => bucket.CategoryKey == category);
        }

        if (filter.Channel is { } channel)
        {
            query = query.Where(bucket => bucket.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(filter.TemplateKey))
        {
            var template = filter.TemplateKey.Trim().ToLowerInvariant();
            query = query.Where(bucket => bucket.TemplateKey == template);
        }

        return await query
            .OrderByDescending(bucket => bucket.Day)
            .Take(Math.Clamp(filter.Limit ?? DefaultQueryLimit, 1, DefaultQueryLimit))
            .ToListAsync(cancellationToken);
    }

    public void Update(NotificationAnalytics analytics)
    {
        ArgumentNullException.ThrowIfNull(analytics);

        NotificationAggregateUpdater.MarkDirty(_dbContext, analytics);
    }
}
