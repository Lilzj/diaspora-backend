using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Conversations.Infrastructure.Community;

/// <summary>
/// Makes one direct message reportable through B6, and nothing more than reportable.
/// <para>
/// <b>The gap this closes was the worst one on the platform.</b> A member could report a guide, a
/// listing, a property, an event, a post, a comment — and could not report a message somebody sent
/// them. <c>CommunityContentType.Message</c> did not exist, <c>/conversations/reports/reasons</c>
/// answered 404 with no error code, and the client carried a sentence saying so. Direct messages are
/// where harassment happens, and they were the one surface with no way to raise it.
/// </para>
///
/// <para>
/// ── <b>Why this is not simply "another content source"</b> ──
/// </para>
/// <para>
/// Every other type B6 describes is public or semi-public. A direct message is neither, and a
/// careless implementation here would turn a reporting feature into a way to read other people's
/// conversations by identifier. Three rules make that impossible, and none of them is optional.
/// </para>
/// <list type="number">
/// <item>
/// <b>Only a participant can resolve a message.</b> <see cref="DescribeAsync"/> loads the
/// conversation <em>for the caller</em> through <see cref="IConversationRepository.GetForParticipantAsync"/>
/// and returns null for anybody else. A non-participant asking about a message id gets exactly what
/// they get for an id that does not exist — <c>community.content.not-found</c> — so the route cannot
/// be used to probe whether a given message is real.
/// </item>
/// <item>
/// <b>The body never leaves this module.</b> <see cref="ContentDescriptor.Title"/> is the neutral
/// word "Message". B6's moderation queue renders titles, and a queue that rendered the text of
/// private messages would publish, to every moderator, conversations nobody consented to share.
/// A moderator who needs the content has to go through a path that records that they did; this seam
/// is not that path.
/// </item>
/// <item>
/// <b>Always <see cref="VisibilityLevel.Private"/>, never commentable.</b> There is no audience for
/// a direct message beyond the people already in it.
/// </item>
/// </list>
///
/// <para>
/// ── <b>What that costs, stated rather than hidden</b> ──
/// </para>
/// <para>
/// <see cref="DescribeManyAsync"/> resolves <b>nothing</b>. B6 calls it to hydrate a moderation
/// queue, and hydrating a page of message reports would need a caller identity the batch signature
/// does not carry — so the honest answer is an empty dictionary, and a message report renders in the
/// queue as an unresolved reference. That is a real limitation of this design and it is the right
/// side to err on: an unhydrated row is an inconvenience for a moderator, and a hydrated one would
/// be a privacy failure.
/// </para>
/// <para>
/// <b>Moderation is deliberately not applied.</b> Withdrawing a message is the sender's own action
/// and a conversation-level decision; a community moderator upholding a report must not reach into
/// somebody's inbox through this seam. B6 records the decision and raises its event, which is what
/// routes the case to the people who can act on it.
/// </para>
/// </summary>
public sealed class MessageContentSource : ICommunityContentSource
{
    private readonly IConversationMessageRepository _messages;
    private readonly IConversationRepository _conversations;
    private readonly ICurrentUser _currentUser;

    public MessageContentSource(
        IConversationMessageRepository messages,
        IConversationRepository conversations,
        ICurrentUser currentUser)
    {
        _messages = messages;
        _conversations = conversations;
        _currentUser = currentUser;
    }

    public CommunityContentType ContentType => CommunityContentType.Message;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        if (contentId == Guid.Empty)
        {
            return null;
        }

        var callerId = _currentUser.UserId;

        if (callerId is null)
        {
            return null;
        }

        var message = await _messages.GetAsync(
            ConversationMessageId.From(contentId), cancellationToken);

        if (message is null)
        {
            return null;
        }

        // The membership check, and the reason it is a *load* rather than a comparison: a caller
        // who is not in this conversation must not have its participant list read into memory on
        // their behalf. `GetForParticipantAsync` answers null, and null is indistinguishable from
        // "no such message" all the way out to the HTTP status.
        var conversation = await _conversations.GetForParticipantAsync(
            message.ConversationId, callerId, cancellationToken);

        return conversation is null ? null : Describe(message);
    }

    /// <summary>
    /// Resolves nothing, on purpose. See the class note.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        return Task.FromResult<IReadOnlyDictionary<Guid, ContentDescriptor>>(
            new Dictionary<Guid, ContentDescriptor>());
    }

    private static ContentDescriptor Describe(ConversationMessage message) => new()
    {
        Reference = ContentReference.Create(CommunityContentType.Message, message.Id.Value),

        // The sender is who the report is about. A system message has no sender, and
        // `Guid.Empty` says so rather than attributing it to somebody.
        AuthorId = message.SenderId?.Value ?? Guid.Empty,

        // Never the body. See the class note.
        Title = "Message",

        // A withdrawn message is still described. Somebody who sends abuse and deletes it has not
        // undone it, and a report filed a minute later must still land.
        Exists = true,
        Visibility = VisibilityLevel.Private,
        AcceptsComments = false
    };
}
