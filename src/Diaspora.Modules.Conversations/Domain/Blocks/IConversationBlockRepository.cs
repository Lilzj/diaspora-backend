using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Conversations.Domain.Blocks;

/// <summary>
/// The block list.
/// <para>
/// <see cref="AreBlockedAsync"/> is the method every write path calls, and it asks about
/// <em>either</em> direction on purpose: A must not be able to message B whether A blocked B or B
/// blocked A. Asking directionally is the mistake that lets a blocked member keep sending, because
/// the check was written from the sender's point of view.
/// </para>
/// </summary>
public interface IConversationBlockRepository
{
    /// <summary>Whether a block exists in either direction between two members.</summary>
    Task<bool> AreBlockedAsync(UserId first, UserId second, CancellationToken cancellationToken = default);

    /// <summary>The specific row, for removal. Directional: only the blocker's own row.</summary>
    Task<ConversationBlock?> FindAsync(
        UserId blockerUserId, UserId blockedUserId, CancellationToken cancellationToken = default);

    /// <summary>Everybody this member has blocked, newest first.</summary>
    Task<IReadOnlyList<ConversationBlock>> ListForBlockerAsync(
        UserId blockerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of these members are blocked in either direction relative to the viewer.
    /// <para>
    /// Batched because an inbox render asks about every counterpart on the page at once, and the
    /// per-row alternative is the N+1 this phase is required not to ship.
    /// </para>
    /// </summary>
    Task<IReadOnlySet<Guid>> FilterBlockedAsync(
        UserId viewer, IReadOnlyCollection<UserId> others, CancellationToken cancellationToken = default);

    void Add(ConversationBlock block);

    void Remove(ConversationBlock block);
}
