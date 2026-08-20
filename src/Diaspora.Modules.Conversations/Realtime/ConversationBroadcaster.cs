using Diaspora.Modules.Conversations.Application.Mapping;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Observability;
using Diaspora.Modules.Conversations.Options;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Conversations.Realtime;

/// <summary>
/// Every live write this module makes, in one class.
/// <para>
/// <b>It names no transport.</b> Everything here goes through <see cref="IRealtimeNotifier"/>, B13's
/// two-method port — send to a member, send to a group. This assembly has no reference to SignalR
/// and an architecture test says so; swapping the platform onto a hosted realtime service replaces
/// one class in the Communication Platform and nothing here.
/// </para>
/// <para>
/// <b>Nothing in here can fail a write.</b> Every method swallows, because a live update is an
/// optimisation on top of a fact already committed: the message is in the database, the recipient's
/// unread count is correct, and push exists for exactly the member whose socket was not there. A
/// broadcaster that threw would roll back a conversation because somebody's phone was in a tunnel.
/// </para>
/// <para>
/// <b>Two audiences, deliberately kept apart.</b> A message goes to the conversation group — anybody
/// looking at the thread sees it appear. A state change goes to the member's own group — archiving
/// is a private filing decision, and broadcasting it would tell the other party you had put them
/// away. Getting that backwards is a privacy leak that looks like a working feature.
/// </para>
/// </summary>
public sealed class ConversationBroadcaster
{
    private readonly IRealtimeNotifier _notifier;
    private readonly IConversationRepository _conversations;
    private readonly IOptionsMonitor<ConversationOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<ConversationBroadcaster> _logger;

    public ConversationBroadcaster(
        IRealtimeNotifier notifier,
        IConversationRepository conversations,
        IOptionsMonitor<ConversationOptions> options,
        IClock clock,
        ILogger<ConversationBroadcaster> logger)
    {
        _notifier = notifier;
        _conversations = conversations;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    private bool IsLive => _options.CurrentValue.EnableRealtime && _notifier.IsEnabled;

    /// <summary>A message arrived. Raised under B13's own <c>MessageReceived</c> name.</summary>
    public Task MessageSentAsync(
        ConversationMessage message,
        IReadOnlyList<UserId> recipients,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(recipients);

        var payload = ToPayload(message);

        return SafelyAsync(async () =>
        {
            // ── One event per member, addressed to the member ──────────────────────
            //
            // This used to write twice: once to `conversation:{id}` for anyone with the
            // thread open, and once to each recipient's own group for anyone whose inbox
            // was open but who had not joined the conversation. A participant in both —
            // the ordinary case for whoever is actually reading the thread — received
            // `MessageReceived` **twice for one message**, and the client had to
            // de-duplicate on messageId to avoid rendering it twice.
            //
            // The conversation-group write turns out to add nothing. `RealtimeHub`
            // server-joins every connection to its own user group on connect, and
            // `SendToUserAsync` addresses that group, so a member's user group already
            // reaches *every socket they hold* whether or not they joined this
            // conversation. Anyone the group write could reach is someone the user write
            // reaches anyway.
            //
            // The one thing the group write did carry on its own was the sender's other
            // devices, because the sender is deliberately absent from `recipients` — the
            // conversation marks them read-through instead. So the sender is addressed
            // explicitly here rather than left to depend on having joined a group.
            //
            // Net effect: exactly one `MessageReceived` per member per message, all
            // devices still served, the open-thread and inbox cases both served by the
            // same event, and the client's de-duplication kept as defence rather than as
            // a requirement.
            foreach (var recipient in recipients)
            {
                await _notifier.SendToUserAsync(
                    recipient, RealtimeEvents.MessageReceived, payload, cancellationToken);

                await SendUnreadAsync(recipient, cancellationToken);
            }

            // The sender's own other devices. No unread write: they have read what they
            // just wrote, which is why they are not in `recipients` to begin with.
            //
            // `SenderId` is null on a system message — that is what `IsSystem` reads — and a
            // system message has no author with devices to catch up. Skipping is the whole
            // handling it needs.
            if (message.SenderId is { } sender
                && !recipients.Any(recipient => recipient == sender))
            {
                await _notifier.SendToUserAsync(
                    sender, RealtimeEvents.MessageReceived, payload, cancellationToken);
            }

            ConversationTelemetry.RealtimeBroadcasts.Add(
                1, new KeyValuePair<string, object?>("event", RealtimeEvents.MessageReceived));
        });
    }

    public Task MessageEditedAsync(
        ConversationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return SendToConversationAsync(
            message.ConversationId.Value,
            ConversationRealtimeEvents.MessageEdited,
            ToPayload(message),
            cancellationToken);
    }

    public Task MessageDeletedAsync(
        ConversationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return SendToConversationAsync(
            message.ConversationId.Value,
            ConversationRealtimeEvents.MessageDeleted,
            ToPayload(message),
            cancellationToken);
    }

    /// <summary>A conversation appeared. To each participant's own group — nobody has joined yet.</summary>
    public Task ConversationCreatedAsync(
        Conversation conversation,
        IReadOnlyList<UserId> participants,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(participants);

        return SafelyAsync(async () =>
        {
            foreach (var participant in participants)
            {
                var row = conversation.ParticipantFor(participant);

                var payload = new ConversationCreatedPayload(
                    conversation.Id.Value,
                    conversation.Context.Type.Key,
                    conversation.Context.EntityId,
                    conversation.Participants
                        .FirstOrDefault(candidate =>
                            candidate.Role == Domain.Enums.ParticipantRole.Initiator)?
                        .UserId.Value ?? Guid.Empty,

                    // Resolved per participant, because the initiator and the recipient of a request
                    // see different states — see ConversationStatePolicy.
                    ConversationWireNames.ToWire(
                        ConversationStatePolicy.Resolve(
                            conversation, row, isBlocked: false, _clock.UtcNow)),
                    conversation.CreatedAt);

                await _notifier.SendToUserAsync(
                    participant,
                    ConversationRealtimeEvents.ConversationCreated,
                    payload,
                    cancellationToken);
            }
        });
    }

    /// <summary>
    /// One member's archive, mute or pin moved. To that member only.
    /// </summary>
    public Task StateChangedAsync(
        Conversation conversation, UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(userId);

        var participant = conversation.ParticipantFor(userId);

        if (participant is null)
        {
            return Task.CompletedTask;
        }

        var now = _clock.UtcNow;

        var payload = new ConversationStateChangedPayload(
            conversation.Id.Value,
            ConversationWireNames.ToWire(
                ConversationStatePolicy.Resolve(conversation, participant, isBlocked: false, now)),
            participant.IsArchived,
            participant.IsMuted,
            participant.IsPinned,
            participant.MutedUntil,
            now);

        return SafelyAsync(async () =>
        {
            await _notifier.SendToUserAsync(
                userId,
                ConversationRealtimeEvents.ConversationStateChanged,
                payload,
                cancellationToken);

            await SendUnreadAsync(userId, cancellationToken);
        });
    }

    public Task ConversationClosedAsync(
        Conversation conversation,
        IReadOnlyList<UserId> participants,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(participants);

        var payload = new ConversationClosedPayload(
            conversation.Id.Value, reason, conversation.ClosedAt ?? _clock.UtcNow);

        return SafelyAsync(async () =>
        {
            // Both audiences. The group reaches anybody with the thread open so the composer
            // disables; the user groups reach the rest so the inbox row updates.
            await _notifier.SendToGroupAsync(
                RealtimeGroups.ForConversation(conversation.Id.Value),
                ConversationRealtimeEvents.ConversationClosed,
                payload,
                cancellationToken);

            foreach (var participant in participants)
            {
                await _notifier.SendToUserAsync(
                    participant,
                    ConversationRealtimeEvents.ConversationClosed,
                    payload,
                    cancellationToken);
            }
        });
    }

    /// <summary>
    /// A read receipt, on behalf of the reader.
    /// <para>
    /// The hub relays these too, when the client calls <c>MarkRead</c> over the socket. This is the
    /// REST path's equivalent, and both are needed: a client that marks read by HTTP — which is what
    /// the shipped client does on opening a conversation — must still turn the sender's ticks blue.
    /// </para>
    /// </summary>
    public Task ReadReceiptAsync(
        Guid conversationId,
        Guid messageId,
        UserId reader,
        DateTimeOffset readAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return SendToConversationAsync(
            conversationId,
            RealtimeEvents.ReadReceiptReceived,
            new ReadReceipt(conversationId, messageId, reader.Value, readAt),
            cancellationToken);
    }

    /// <summary>The member's conversation unread total, pushed rather than polled.</summary>
    public Task SendUnreadAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return SafelyAsync(async () =>
        {
            var now = _clock.UtcNow;
            var total = await _conversations.CountUnreadAsync(userId, now, cancellationToken);

            await _notifier.SendToUserAsync(
                userId,
                ConversationRealtimeEvents.ConversationUnreadChanged,
                new ConversationUnreadPayload(total, now),
                cancellationToken);
        });
    }

    private Task SendToConversationAsync(
        Guid conversationId, string eventName, object payload, CancellationToken cancellationToken) =>
        SafelyAsync(async () =>
        {
            await _notifier.SendToGroupAsync(
                RealtimeGroups.ForConversation(conversationId),
                eventName,
                payload,
                cancellationToken);

            ConversationTelemetry.RealtimeBroadcasts.Add(
                1, new KeyValuePair<string, object?>("event", eventName));
        });

    private static ConversationMessagePayload ToPayload(ConversationMessage message) =>
        new(
            message.ConversationId.Value,
            message.Id.Value,
            message.SenderId?.Value ?? Guid.Empty,
            ConversationWireNames.ToWire(message.Kind),
            message.Body,
            message.Attachment is not null,
            message.ReplyTo?.MessageId.Value,
            message.SentAt,
            message.ClientMessageId);

    /// <summary>
    /// Runs a broadcast and swallows anything but cancellation.
    /// <para>
    /// The fact is already committed. See the class summary for why this must never rethrow.
    /// </para>
    /// </summary>
    private async Task SafelyAsync(Func<Task> broadcast)
    {
        if (!IsLive)
        {
            return;
        }

        try
        {
            await broadcast();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A live update is best-effort by definition. See the class summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "A conversation real-time broadcast failed. The change is committed and clients "
                + "will see it on their next fetch.");
        }
#pragma warning restore CA1031
    }
}
