using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Notifications.Extensibility.Search;

/// <summary>
/// Finds messages in a member's inbox.
/// <para>
/// <b>An abstraction with an EF implementation behind it, exactly as the brief asks: the OpenSearch
/// adapter is designed for and not written.</b> The shipped engine queries the write model, which is
/// correct, transactional and entirely adequate for an inbox — a member's messages are a bounded set
/// scoped by an indexed identifier, not a corpus.
/// </para>
/// <para>
/// The port exists because two things eventually stop being adequate: relevance ranking across a
/// long history, and phrase matching in languages whose word boundaries a <c>LIKE</c> does not
/// respect. Both are index problems, and both arrive as a second implementation of this interface
/// with no change to any handler.
/// </para>
/// <para>
/// It returns aggregates rather than projections deliberately: the caller maps once, and a future
/// engine returning identifiers can rehydrate through the repository without changing the contract.
/// </para>
/// </summary>
public interface INotificationSearchEngine
{
    Task<CursorPagedResult<Notification>> SearchAsync(
        NotificationQueryFilter filter, CancellationToken cancellationToken = default);
}
