using Diaspora.Application.Abstractions;
using Diaspora.Modules.Conversations.Application.Mapping;
using Diaspora.Modules.Conversations.Application.Services;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Events;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Realtime;

namespace Diaspora.Modules.Conversations.Application.EventHandlers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// These run after the transaction commits. That ordering is the whole reason they exist as separate
// handlers rather than as code at the end of each command: a realtime write or a push issued inside
// the transaction would announce something a rollback then un-did, and the client would render a
// message that is not in the database. The dispatcher runs them after SaveChanges, so by the time
// any of this executes the fact is true.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A conversation was opened: tell the participants and publish the fact.</summary>
public sealed class ConversationCreatedEventHandler
    : DomainEventHandler<ConversationCreatedDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly ConversationBroadcaster _broadcaster;
    private readonly IConversationRepository _conversations;

    public ConversationCreatedEventHandler(
        ConversationIntegrationEventRelay relay,
        ConversationBroadcaster broadcaster,
        IConversationRepository conversations)
    {
        _relay = relay;
        _broadcaster = broadcaster;
        _conversations = conversations;
    }

    protected override async Task HandleAsync(
        ConversationCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        // Reloaded rather than carried on the event: the broadcast needs participant rows to compute
        // each recipient's state, and an aggregate on an event is an aggregate somebody will mutate.
        var conversation = await _conversations.GetAsync(domainEvent.ConversationId, cancellationToken);

        if (conversation is not null)
        {
            await _broadcaster.ConversationCreatedAsync(
                conversation, domainEvent.ParticipantUserIds, cancellationToken);
        }
    }
}

/// <summary>
/// A message was sent: fan it out live, then decide who should be interrupted.
/// <para>
/// Realtime first and notification second, deliberately. A member with the app open should see the
/// message before their phone considers buzzing about it, and the notification path is the slower of
/// the two — it consults preferences, quiet hours and a throttle.
/// </para>
/// </summary>
public sealed class MessageSentEventHandler : DomainEventHandler<MessageSentDomainEvent>
{
    private readonly ConversationBroadcaster _broadcaster;
    private readonly ConversationNotifier _notifier;
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly IConversationRepository _conversations;
    private readonly IConversationMessageRepository _messages;

    public MessageSentEventHandler(
        ConversationBroadcaster broadcaster,
        ConversationNotifier notifier,
        ConversationIntegrationEventRelay relay,
        IConversationRepository conversations,
        IConversationMessageRepository messages)
    {
        _broadcaster = broadcaster;
        _notifier = notifier;
        _relay = relay;
        _conversations = conversations;
        _messages = messages;
    }

    protected override async Task HandleAsync(
        MessageSentDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var message = await _messages.GetAsync(domainEvent.MessageId, cancellationToken);

        if (message is null)
        {
            return;
        }

        await _broadcaster.MessageSentAsync(
            message, domainEvent.RecipientUserIds, cancellationToken);

        var conversation = await _conversations.GetAsync(
            domainEvent.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return;
        }

        // Before the notifier, and independent of it: whether anybody is told about this message
        // has no bearing on whether it was the first one written here.
        await _relay.PublishFirstMessageAsync(domainEvent, conversation, cancellationToken);

        // The notifier applies mute and block and publishes an integration event naming only the
        // people who should hear about it. It does not create notifications — see DSP2003.
        await _notifier.MessageSentAsync(
            conversation,
            message,
            domainEvent.RecipientUserIds,
            domainEvent.ReplyToSenderId,
            cancellationToken);
    }
}

public sealed class MessageEditedEventHandler : DomainEventHandler<MessageEditedDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly ConversationBroadcaster _broadcaster;
    private readonly IConversationMessageRepository _messages;

    public MessageEditedEventHandler(
        ConversationIntegrationEventRelay relay,
        ConversationBroadcaster broadcaster,
        IConversationMessageRepository messages)
    {
        _relay = relay;
        _broadcaster = broadcaster;
        _messages = messages;
    }

    protected override async Task HandleAsync(
        MessageEditedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (await _messages.GetAsync(domainEvent.MessageId, cancellationToken) is { } message)
        {
            await _broadcaster.MessageEditedAsync(message, cancellationToken);
        }
    }
}

public sealed class MessageDeletedEventHandler : DomainEventHandler<MessageDeletedDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly ConversationBroadcaster _broadcaster;
    private readonly IConversationMessageRepository _messages;

    public MessageDeletedEventHandler(
        ConversationIntegrationEventRelay relay,
        ConversationBroadcaster broadcaster,
        IConversationMessageRepository messages)
    {
        _relay = relay;
        _broadcaster = broadcaster;
        _messages = messages;
    }

    protected override async Task HandleAsync(
        MessageDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (await _messages.GetAsync(domainEvent.MessageId, cancellationToken) is { } message)
        {
            await _broadcaster.MessageDeletedAsync(message, cancellationToken);
        }
    }
}

/// <summary>
/// Somebody read: turn the sender's ticks blue and correct the reader's badge.
/// <para>
/// The receipt goes to the conversation group even when the read cleared nothing. A sender who was
/// offline the first time still needs telling, and "nothing was cleared" says only that this reader
/// had already read it — not that the sender knows.
/// </para>
/// </summary>
public sealed class MessagesReadEventHandler : DomainEventHandler<MessagesReadDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly ConversationBroadcaster _broadcaster;

    public MessagesReadEventHandler(
        ConversationIntegrationEventRelay relay, ConversationBroadcaster broadcaster)
    {
        _relay = relay;
        _broadcaster = broadcaster;
    }

    protected override async Task HandleAsync(
        MessagesReadDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (domainEvent.ThroughMessageId is { } messageId)
        {
            await _broadcaster.ReadReceiptAsync(
                domainEvent.ConversationId.Value,
                messageId.Value,
                domainEvent.ReaderUserId,
                domainEvent.ReadAt,
                cancellationToken);
        }

        await _broadcaster.SendUnreadAsync(domainEvent.ReaderUserId, cancellationToken);
    }
}

/// <summary>
/// Archive, restore, mute and pin, all four to the acting member's own devices only.
/// <para>
/// <b>Never to the conversation group.</b> Filing somebody away is private; broadcasting it would
/// tell the other party you had archived them, which is a disclosure nobody asked for and the
/// client has no way to render kindly.
/// </para>
/// </summary>
public sealed class ConversationArchivedEventHandler
    : DomainEventHandler<ConversationArchivedDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly ConversationBroadcaster _broadcaster;
    private readonly IConversationRepository _conversations;

    public ConversationArchivedEventHandler(
        ConversationIntegrationEventRelay relay,
        ConversationBroadcaster broadcaster,
        IConversationRepository conversations)
    {
        _relay = relay;
        _broadcaster = broadcaster;
        _conversations = conversations;
    }

    protected override async Task HandleAsync(
        ConversationArchivedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (await _conversations.GetAsync(domainEvent.ConversationId, cancellationToken) is { } row)
        {
            await _broadcaster.StateChangedAsync(row, domainEvent.UserId, cancellationToken);
        }
    }
}

public sealed class ConversationRestoredEventHandler
    : DomainEventHandler<ConversationRestoredDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly ConversationBroadcaster _broadcaster;
    private readonly IConversationRepository _conversations;

    public ConversationRestoredEventHandler(
        ConversationIntegrationEventRelay relay,
        ConversationBroadcaster broadcaster,
        IConversationRepository conversations)
    {
        _relay = relay;
        _broadcaster = broadcaster;
        _conversations = conversations;
    }

    protected override async Task HandleAsync(
        ConversationRestoredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (await _conversations.GetAsync(domainEvent.ConversationId, cancellationToken) is { } row)
        {
            await _broadcaster.StateChangedAsync(row, domainEvent.UserId, cancellationToken);
        }
    }
}

public sealed class ConversationMutedEventHandler : DomainEventHandler<ConversationMutedDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly ConversationBroadcaster _broadcaster;
    private readonly IConversationRepository _conversations;

    public ConversationMutedEventHandler(
        ConversationIntegrationEventRelay relay,
        ConversationBroadcaster broadcaster,
        IConversationRepository conversations)
    {
        _relay = relay;
        _broadcaster = broadcaster;
        _conversations = conversations;
    }

    protected override async Task HandleAsync(
        ConversationMutedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (await _conversations.GetAsync(domainEvent.ConversationId, cancellationToken) is { } row)
        {
            await _broadcaster.StateChangedAsync(row, domainEvent.UserId, cancellationToken);
        }
    }
}

public sealed class ConversationPinnedEventHandler
    : DomainEventHandler<ConversationPinnedDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly ConversationBroadcaster _broadcaster;
    private readonly IConversationRepository _conversations;

    public ConversationPinnedEventHandler(
        ConversationIntegrationEventRelay relay,
        ConversationBroadcaster broadcaster,
        IConversationRepository conversations)
    {
        _relay = relay;
        _broadcaster = broadcaster;
        _conversations = conversations;
    }

    protected override async Task HandleAsync(
        ConversationPinnedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (await _conversations.GetAsync(domainEvent.ConversationId, cancellationToken) is { } row)
        {
            await _broadcaster.StateChangedAsync(row, domainEvent.UserId, cancellationToken);
        }
    }
}

/// <summary>A conversation ended: tell everybody, and publish it for Marketplace and B13.</summary>
public sealed class ConversationClosedEventHandler
    : DomainEventHandler<ConversationClosedDomainEvent>
{
    private readonly ConversationBroadcaster _broadcaster;
    private readonly ConversationNotifier _notifier;
    private readonly IConversationRepository _conversations;

    public ConversationClosedEventHandler(
        ConversationBroadcaster broadcaster,
        ConversationNotifier notifier,
        IConversationRepository conversations)
    {
        _broadcaster = broadcaster;
        _notifier = notifier;
        _conversations = conversations;
    }

    protected override async Task HandleAsync(
        ConversationClosedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetAsync(
            domainEvent.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return;
        }

        await _broadcaster.ConversationClosedAsync(
            conversation, domainEvent.ParticipantUserIds, domainEvent.Reason, cancellationToken);

        // The notifier publishes ConversationClosedIntegrationEvent itself, having applied mute —
        // so there is no relay call here. Publishing from both would produce two outbox rows and two
        // notifications for one closure.
        await _notifier.ConversationClosedAsync(
            conversation, domainEvent.ParticipantUserIds, domainEvent.Reason, cancellationToken);
    }
}

/// <summary>Somebody was added: tell them, and let B13 invite them.</summary>
public sealed class ParticipantAddedEventHandler : DomainEventHandler<ParticipantAddedDomainEvent>
{
    private readonly ConversationNotifier _notifier;
    private readonly ConversationBroadcaster _broadcaster;
    private readonly IConversationRepository _conversations;

    public ParticipantAddedEventHandler(
        ConversationNotifier notifier,
        ConversationBroadcaster broadcaster,
        IConversationRepository conversations)
    {
        _notifier = notifier;
        _broadcaster = broadcaster;
        _conversations = conversations;
    }

    protected override async Task HandleAsync(
        ParticipantAddedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetAsync(
            domainEvent.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return;
        }

        await _broadcaster.ConversationCreatedAsync(
            conversation, [domainEvent.UserId], cancellationToken);

        await _notifier.ParticipantAddedAsync(
            conversation,
            domainEvent.UserId,
            ConversationWireNames.ToWire(domainEvent.Role),
            cancellationToken);
    }
}

public sealed class ParticipantRemovedEventHandler
    : DomainEventHandler<ParticipantRemovedDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly ConversationBroadcaster _broadcaster;

    public ParticipantRemovedEventHandler(
        ConversationIntegrationEventRelay relay, ConversationBroadcaster broadcaster)
    {
        _relay = relay;
        _broadcaster = broadcaster;
    }

    protected override async Task HandleAsync(
        ParticipantRemovedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        // Their badge has to move: the unread count they were carrying for this conversation is
        // gone, and nothing else would tell their client.
        await _broadcaster.SendUnreadAsync(domainEvent.UserId, cancellationToken);
    }
}

public sealed class ConversationRequestAcceptedEventHandler
    : DomainEventHandler<ConversationRequestAcceptedDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;
    private readonly ConversationBroadcaster _broadcaster;
    private readonly IConversationRepository _conversations;

    public ConversationRequestAcceptedEventHandler(
        ConversationIntegrationEventRelay relay,
        ConversationBroadcaster broadcaster,
        IConversationRepository conversations)
    {
        _relay = relay;
        _broadcaster = broadcaster;
        _conversations = conversations;
    }

    protected override async Task HandleAsync(
        ConversationRequestAcceptedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (await _conversations.GetAsync(domainEvent.ConversationId, cancellationToken) is { } row)
        {
            // Both parties: the initiator learns their request was taken up, the accepter's own row
            // moves out of the request inbox.
            await _broadcaster.ConversationCreatedAsync(
                row,
                [domainEvent.InitiatorUserId, domainEvent.AcceptedByUserId],
                cancellationToken);
        }
    }
}

public sealed class ConversationBlockedEventHandler
    : DomainEventHandler<ConversationBlockedDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;

    public ConversationBlockedEventHandler(ConversationIntegrationEventRelay relay) => _relay = relay;

    /// <summary>
    /// Published, and deliberately not broadcast.
    /// <para>
    /// The blocked member is never told. A realtime event would arrive on their socket and any
    /// client that logged unknown frames would have disclosed it.
    /// </para>
    /// </summary>
    protected override Task HandleAsync(
        ConversationBlockedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class ConversationUnblockedEventHandler
    : DomainEventHandler<ConversationUnblockedDomainEvent>
{
    private readonly ConversationIntegrationEventRelay _relay;

    public ConversationUnblockedEventHandler(ConversationIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        ConversationUnblockedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
