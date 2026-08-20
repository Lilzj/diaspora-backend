using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Notifications.Domain.Templates;

/// <summary>
/// The template catalogue's persistence port.
/// <para>
/// <see cref="FindPublishedAsync"/> is the one the dispatcher calls, once per notification, and it
/// is the reason the render cache exists: the same handful of templates are resolved thousands of
/// times an hour and none of them changes between publications.
/// </para>
/// </summary>
public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetAsync(
        NotificationTemplateId id, CancellationToken cancellationToken = default);

    /// <summary>The usable version of a template, or null when nothing is published under that key.</summary>
    Task<NotificationTemplate?> FindPublishedAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>Every version under one key, newest first. The authoring screen's history panel.</summary>
    Task<IReadOnlyList<NotificationTemplate>> ListVersionsAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>The current draft under a key, if one is open.</summary>
    Task<NotificationTemplate?> FindDraftAsync(
        string key, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<NotificationTemplate>> SearchAsync(
        TemplateQueryFilter filter, CancellationToken cancellationToken = default);

    Task<int> HighestVersionAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(NotificationTemplate template, CancellationToken cancellationToken = default);

    void Update(NotificationTemplate template);
}
