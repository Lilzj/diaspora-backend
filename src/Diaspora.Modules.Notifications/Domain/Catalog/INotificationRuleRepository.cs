namespace Diaspora.Modules.Notifications.Domain.Catalog;

/// <summary>
/// The routing catalogue's persistence port.
/// <para>
/// <see cref="ListForEventAsync"/> is called once per consumed integration event and returns every
/// rule that matches — a single event may address several audiences with different templates and
/// urgencies. It returns a list rather than one rule for exactly that reason.
/// </para>
/// </summary>
public interface INotificationRuleRepository
{
    Task<NotificationRule?> FindByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationRule>> ListForEventAsync(
        string eventName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationRule>> ListAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(NotificationRule rule, CancellationToken cancellationToken = default);

    void Update(NotificationRule rule);
}
