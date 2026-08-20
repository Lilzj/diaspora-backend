using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Extensibility.Search;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Notifications.Persistence.Repositories;

/// <summary>
/// The shipped search engine: the write model, queried directly.
/// <para>
/// <b>Correct, transactional, and entirely adequate — which is why the OpenSearch adapter is
/// designed for and not written.</b> A member's inbox is a bounded set behind an indexed identifier,
/// not a corpus, and a containment match over a few thousand rows a member actually owns is a range
/// scan rather than a search problem.
/// </para>
/// <para>
/// The port exists for the two things that eventually stop being adequate: relevance ranking across
/// a long history, and phrase matching in languages whose word boundaries a <c>LIKE</c> does not
/// respect. Both are index problems and both arrive as a second implementation with no change to any
/// handler.
/// </para>
/// </summary>
public sealed class EfNotificationSearchEngine : INotificationSearchEngine
{
    private readonly INotificationRepository _notifications;

    public EfNotificationSearchEngine(INotificationRepository notifications) =>
        _notifications = notifications;

    public Task<CursorPagedResult<Notification>> SearchAsync(
        NotificationQueryFilter filter, CancellationToken cancellationToken = default) =>
        _notifications.SearchAsync(filter, cancellationToken);
}
