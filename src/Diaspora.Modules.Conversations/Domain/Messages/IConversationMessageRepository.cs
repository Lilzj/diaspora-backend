using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Conversations.Domain.Messages;

/// <summary>
/// How messages are read and written.
/// <para>
/// Every read is scoped to a conversation the caller has already been authorised for. There is no
/// "search all messages" method that does not take a member: a full-text search across every thread
/// is the one query in this module that would be catastrophic to get wrong, so the seam that could
/// serve it — <c>IConversationSearchEngine</c> — takes the searcher, and
/// this repository has no such method at all.
/// </para>
/// </summary>
public interface IConversationMessageRepository
{
    Task<ConversationMessage?> GetAsync(
        ConversationMessageId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The message, only if it belongs to the named conversation.
    /// <para>
    /// The conversation is checked here rather than by the caller comparing afterwards, because a
    /// caller that loads a message by id and then compares has already loaded somebody else's
    /// message into memory — and the comparison is the line somebody eventually deletes.
    /// </para>
    /// </summary>
    Task<ConversationMessage?> GetInConversationAsync(
        ConversationId conversationId,
        ConversationMessageId messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of history, newest first.
    /// <para>
    /// Newest first because a conversation opens at the bottom, and a client that had to page
    /// forward from the beginning of a two-year thread would fetch two years to render one screen.
    /// </para>
    /// </summary>
    Task<CursorPagedResult<ConversationMessage>> ListAsync(
        MessageQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every message in a conversation carrying an attachment, for the media gallery.
    /// <para>
    /// Bounded by the same page limit as history. A gallery over a thread with six hundred
    /// photographs is still a gallery over a page of them.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ConversationMessage>> ListWithAttachmentsAsync(
        ConversationId conversationId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything a member sent that has not yet been marked delivered or read, up to a cursor.
    /// <para>
    /// The durable half of receipt handling: a relayed receipt is lost if the sender was offline,
    /// so the sender's next read of the thread has to be able to reconstruct it from the rows.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ConversationMessage>> ListPendingReceiptsAsync(
        ConversationId conversationId,
        UserId excludingSender,
        DateTimeOffset sentAtOrBefore,
        CancellationToken cancellationToken = default);

    /// <summary>Whether this client-supplied identifier has already produced a message.</summary>
    Task<ConversationMessage?> FindByClientIdAsync(
        ConversationId conversationId,
        string clientMessageId,
        CancellationToken cancellationToken = default);

    void Add(ConversationMessage message);

    void Update(ConversationMessage message);
}
