using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Conversations.Persistence.Repositories;

/// <summary>
/// Message history, always by cursor and always scoped to one conversation.
/// </summary>
public sealed class ConversationMessageRepository : IConversationMessageRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ConversationMessageRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ConversationMessage> Messages => _dbContext.Set<ConversationMessage>();

    public async Task<ConversationMessage?> GetAsync(
        ConversationMessageId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Messages.FirstOrDefaultAsync(message => message.Id == id, cancellationToken);
    }

    public async Task<ConversationMessage?> GetInConversationAsync(
        ConversationId conversationId,
        ConversationMessageId messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(messageId);

        return await Messages.FirstOrDefaultAsync(
            message => message.Id == messageId && message.ConversationId == conversationId,
            cancellationToken);
    }

    public async Task<CursorPagedResult<ConversationMessage>> ListAsync(
        MessageQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var conversationId = filter.ConversationId;

        var query = Messages.Where(message => message.ConversationId == conversationId);

        if (!filter.IncludeWithdrawn)
        {
            query = query.Where(message =>
                message.Status != MessageStatus.Deleted && message.Status != MessageStatus.Hidden);
        }

        if (filter.AttachmentsOnly)
        {
            query = query.Where(message => message.Attachment != null);
        }

        var cursor = ConversationCursor.Parse(filter.Cursor);

        if (cursor is not null)
        {
            var boundary = cursor.Timestamp;
            var exclusion = CursorBoundary.Exclusion(cursor.BoundaryKey, ConversationMessageId.From);

            // Set membership, not an ordering comparison on the identifier — see CursorBoundary and
            // the analyser rule behind it. Two messages sent in the same millisecond is the ordinary
            // case here, not a rare tie.
            query = exclusion.IsExhausted
                ? query.Where(message => message.SentAt < boundary)
                : query.Where(message =>
                    message.SentAt < boundary
                    || (message.SentAt == boundary && !exclusion.Ids.Contains(message.Id)));
        }

        var limit = filter.EffectiveLimit;

        var page = await query
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > limit;
        var items = hasMore ? page[..limit] : page;

        var next = hasMore && items.Count > 0
            ? ConversationCursor
                .Create(
                    items[^1].SentAt,
                    CursorBoundary.Key(
                        items,
                        message => message.SentAt,
                        message => message.Id.Value,
                        cursor?.BoundaryKey,
                        cursor?.Timestamp))
                .Encode()
            : null;

        return CursorPagedResult<ConversationMessage>.Create(items, next, hasMore);
    }

    public async Task<IReadOnlyList<ConversationMessage>> ListWithAttachmentsAsync(
        ConversationId conversationId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversationId);

        return await Messages
            .Where(message =>
                message.ConversationId == conversationId
                && message.Attachment != null
                && message.Status != MessageStatus.Deleted
                && message.Status != MessageStatus.Hidden)
            .OrderByDescending(message => message.SentAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationMessage>> ListPendingReceiptsAsync(
        ConversationId conversationId,
        UserId excludingSender,
        DateTimeOffset sentAtOrBefore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(excludingSender);

        return await Messages
            .Where(message =>
                message.ConversationId == conversationId
                && message.SenderId != null
                && message.SenderId != excludingSender
                && message.SentAt <= sentAtOrBefore
                && message.Status != MessageStatus.Read
                && message.Status != MessageStatus.Deleted
                && message.Status != MessageStatus.Hidden)
            .OrderBy(message => message.SentAt)

            // Bounded. A member opening a thread they have ignored for a month must not turn one
            // read into four thousand updates inside a request.
            .Take(ConversationPersistenceLimits.MaxReceiptBatch)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversationMessage?> FindByClientIdAsync(
        ConversationId conversationId,
        string clientMessageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversationId);

        if (string.IsNullOrWhiteSpace(clientMessageId))
        {
            return null;
        }

        return await Messages.FirstOrDefaultAsync(
            message => message.ConversationId == conversationId
                       && message.ClientMessageId == clientMessageId,
            cancellationToken);
    }

    public void Add(ConversationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Messages.Add(message);
    }

    public void Update(ConversationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_dbContext.Entry(message).State == EntityState.Detached)
        {
            Messages.Update(message);
        }
    }
}

/// <summary>Bounds on work a single request may do.</summary>
internal static class ConversationPersistenceLimits
{
    /// <summary>How many messages one read can flip to delivered or read.</summary>
    public const int MaxReceiptBatch = 200;

    /// <summary>How many attachments the media gallery returns.</summary>
    public const int MaxGalleryItems = 100;
}
