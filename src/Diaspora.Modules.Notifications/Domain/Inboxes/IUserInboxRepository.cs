using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Domain.Inboxes;

/// <summary>
/// The inbox counter aggregate's persistence port.
/// <para>
/// <see cref="GetOrCreateAsync"/> rather than a separate create, because every caller wants the same
/// thing — the member's counters, whether or not a row exists yet — and a two-step "find then
/// create" is a race every notification arriving concurrently would lose.
/// </para>
/// </summary>
public interface IUserInboxRepository
{
    Task<UserInbox?> FindAsync(UserId userId, CancellationToken cancellationToken = default);

    Task<UserInbox> GetOrCreateAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Members whose counters have not been checked since a given moment.</summary>
    Task<IReadOnlyList<UserInbox>> ListForReconciliationAsync(
        DateTimeOffset staleBefore, int limit, CancellationToken cancellationToken = default);

    void Update(UserInbox inbox);
}
