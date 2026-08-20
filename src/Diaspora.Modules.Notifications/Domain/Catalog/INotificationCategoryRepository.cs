using Diaspora.Modules.Notifications.Domain.Identifiers;

namespace Diaspora.Modules.Notifications.Domain.Catalog;

/// <summary>The category catalogue's persistence port.</summary>
public interface INotificationCategoryRepository
{
    Task<NotificationCategory?> GetAsync(
        NotificationCategoryId id, CancellationToken cancellationToken = default);

    Task<NotificationCategory?> FindByKeyAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>Everything a preference screen renders, in position order.</summary>
    Task<IReadOnlyList<NotificationCategory>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationCategory>> ListAllAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(NotificationCategory category, CancellationToken cancellationToken = default);

    void Update(NotificationCategory category);
}
