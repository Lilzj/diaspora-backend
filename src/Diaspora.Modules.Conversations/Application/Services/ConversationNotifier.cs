using Diaspora.Contracts.Conversations.V1.Events;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Observability;
using Diaspora.Modules.Conversations.Options;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Conversations.Application.Services;

/// <summary>
/// Decides who should be interrupted, and publishes.
/// <para>
/// <b>It does not create notifications, and the analyzer pack is right to insist.</b> An earlier
/// draft of this class called B13's notification write path directly, which <c>DSP2003</c> refused:
/// a business module that sends its own notification has taken on channel preferences, quiet hours,
/// digest batching, localisation and retries — none of which it can see. The rule found a real
/// design error, not a formality.
/// </para>
/// <para>
/// The architecture suite scans source text rather than symbols for that rule, so naming the port's
/// type even in a comment fails it. That is the rule being blunt rather than wrong: a comment naming
/// the write path is usually a call site that was commented out.
/// </para>
/// <para>
/// <b>The gates still belong here, and the split is exact.</b> Mute and block are <em>conversation</em>
/// state; B13 has never heard of them and cannot check them. So this class decides <em>whether</em>
/// a recipient should be told, filters them out of the published event, and B13 decides
/// <em>how</em> — which is the boundary the rule describes rather than a way around it.
/// </para>
/// <para>
/// <b>Muting is checked against the clock, not the flag.</b> An eight-hour mute set yesterday leaves
/// the boolean true until something writes to the row, and nothing necessarily does — there is no
/// sweep whose job is to unmute. Asking <c>IsMutedAt</c> is what stops a member being silenced for a
/// week by an eight-hour decision.
/// </para>
/// </summary>
public sealed class ConversationNotifier
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ConversationAccessService _access;
    private readonly IOptionsMonitor<ConversationOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<ConversationNotifier> _logger;

    public ConversationNotifier(
        IIntegrationEventPublisher publisher,
        ConversationAccessService access,
        IOptionsMonitor<ConversationOptions> options,
        IClock clock,
        ILogger<ConversationNotifier> logger)
    {
        _publisher = publisher;
        _access = access;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Publishes a message-sent fact naming only the recipients who should hear about it.
    /// </summary>
    /// <param name="repliedToSender">
    /// The author of the message being answered. They get the reply wording instead of the general
    /// one — being answered directly is a stronger signal than a message arriving, and collapsing
    /// the two would lose it.
    /// </param>
    public async Task MessageSentAsync(
        Conversation conversation,
        ConversationMessage message,
        IReadOnlyList<UserId> recipients,
        UserId? repliedToSender,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(recipients);

        if (!_options.CurrentValue.EnablePushNotifications || message.SenderId is null)
        {
            return;
        }

        var sender = message.SenderId;
        var now = _clock.UtcNow;
        var eligible = new List<Guid>(recipients.Count);

        foreach (var recipient in recipients)
        {
            if (recipient == sender)
            {
                continue;
            }

            if (conversation.ParticipantFor(recipient) is not { IsActive: true } participant)
            {
                continue;
            }

            if (participant.IsMutedAt(now))
            {
                ConversationTelemetry.NotificationsSuppressed.Add(
                    1, new KeyValuePair<string, object?>("reason", "muted"));

                continue;
            }

            // Either direction. Somebody who blocked the sender must not be interrupted by them, and
            // somebody the sender blocked must not learn the sender is still writing.
            if (await _access.AreBlockedAsync(sender, recipient, cancellationToken))
            {
                ConversationTelemetry.NotificationsSuppressed.Add(
                    1, new KeyValuePair<string, object?>("reason", "blocked"));

                continue;
            }

            eligible.Add(recipient.Value);
        }

        if (eligible.Count == 0)
        {
            return;
        }

        await PublishSafelyAsync(
            new ConversationMessageSentIntegrationEvent
            {
                ConversationId = conversation.Id.Value,
                MessageId = message.Id.Value,
                SenderUserId = sender.Value,
                RecipientUserIds = eligible,
                ContextType = conversation.Context.Type.Key,
                EntityId = conversation.Context.EntityId,
                ContextTitle = conversation.Context.EntityTitle,
                MessageKind = Mapping.ConversationWireNames.ToWire(message.Kind),
                HasAttachment = message.Attachment is not null,

                // Only when the person answered is one of the eligible recipients. Naming somebody
                // who was muted or blocked would leak that they are still in the conversation.
                RepliedToUserId = repliedToSender is not null
                                  && eligible.Contains(repliedToSender.Value)
                    ? repliedToSender.Value
                    : null,
                SentAt = message.SentAt
            },
            cancellationToken);
    }

    /// <summary>Publishes that somebody was added, so B13 can invite them.</summary>
    public Task ParticipantAddedAsync(
        Conversation conversation,
        UserId userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(userId);

        return PublishSafelyAsync(
            new ConversationParticipantAddedIntegrationEvent
            {
                ConversationId = conversation.Id.Value,
                UserId = userId.Value,
                Role = role,
                ContextType = conversation.Context.Type.Key,
                ContextTitle = conversation.Context.EntityTitle,
                JoinedAt = _clock.UtcNow
            },
            cancellationToken);
    }

    /// <summary>
    /// Publishes that a conversation ended, naming the participants who are not muted.
    /// <para>
    /// Block is not consulted: nobody is being put in touch with anybody, the platform is reporting
    /// that a thread closed.
    /// </para>
    /// </summary>
    public Task ConversationClosedAsync(
        Conversation conversation,
        IReadOnlyList<UserId> participants,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(participants);

        var now = _clock.UtcNow;

        var eligible = participants
            .Where(participant =>
                conversation.ParticipantFor(participant) is not { } row || !row.IsMutedAt(now))
            .Select(participant => participant.Value)
            .ToList();

        return PublishSafelyAsync(
            new ConversationClosedIntegrationEvent
            {
                ConversationId = conversation.Id.Value,
                ParticipantUserIds = eligible,
                ContextType = conversation.Context.Type.Key,
                EntityId = conversation.Context.EntityId,
                ContextTitle = conversation.Context.EntityTitle,
                Reason = reason,
                ClosedAt = conversation.ClosedAt ?? now
            },
            cancellationToken);
    }

    /// <summary>
    /// Publishes, and never lets a publish failure become a send failure.
    /// <para>
    /// The message is already committed. A conversation that rolled back because the outbox write
    /// hiccuped would lose what somebody typed to save a notification.
    /// </para>
    /// </summary>
    private async Task PublishSafelyAsync(
        Diaspora.Contracts.Events.IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // See the summary: a failed publish is not a failed send.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Publishing a conversation integration event failed. The change is committed and "
                + "participants will see it in the conversation.");
        }
#pragma warning restore CA1031
    }
}
