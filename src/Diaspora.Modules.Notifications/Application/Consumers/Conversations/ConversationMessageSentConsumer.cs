using Diaspora.Contracts.Conversations.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Conversations;

/// <summary>
/// A message arrived in a conversation.
/// <para>
/// <b>The recipient list has already had mute and block applied by the Conversations Platform, and
/// this consumer does not second-guess it.</b> Those are conversation state — per-participant mute,
/// a directional block list — and this module has no reference to the one that owns them. What
/// remains here is what has always been this module's: quiet hours, channel preferences, the
/// category opt-out, throttling, deduplication and localisation. The split is the same one every
/// other consumer in this folder observes; conversations are only unusual in that the upstream
/// filtering is itself a privacy rule rather than a routing one.
/// </para>
/// <para>
/// <b>Grouped on the conversation.</b> Six messages in an evening are one conversation, not six
/// interruptions — the group key collapses them into a single notification that updates, which is
/// the difference between a chat platform and a phone that buzzes all night.
/// </para>
/// <para>
/// <b>No message body reaches a notification.</b> The event does not carry one, deliberately: a
/// notification renders on a lock screen, the one surface with no authentication in front of it.
/// </para>
/// </summary>
public sealed class ConversationMessageSentConsumer
    : ModuleEventConsumer<ConversationMessageSentIntegrationEvent>
{
    /// <summary>
    /// The audience the answered participant is dispatched under, so the reply rule can select a
    /// different template for them without a second event.
    /// </summary>
    private const string RepliedAudience = "replied";

    public ConversationMessageSentConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher)
    {
    }

    protected override string SourceModule => "Conversations";

    protected override IEnumerable<NotificationTrigger> Translate(
        ConversationMessageSentIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        foreach (var recipient in integrationEvent.RecipientUserIds)
        {
            if (recipient == Guid.Empty || recipient == integrationEvent.SenderUserId)
            {
                continue;
            }

            var values = Values();
            values["conversationid"] = integrationEvent.ConversationId.ToString("D");
            values["contexttitle"] = integrationEvent.ContextTitle;
            values["contexttype"] = integrationEvent.ContextType;

            yield return Trigger(
                UserId.From(recipient),
                integrationEvent.RepliedToUserId == recipient ? RepliedAudience : "recipient",
                integrationEvent.SentAt,
                values,
                subjectType: "conversation",
                subjectId: integrationEvent.ConversationId.ToString("D"),

                // The conversation, not the message. Grouping on the message would defeat the point
                // — every message has a different one.
                groupKey: integrationEvent.ConversationId.ToString("D"));
        }
    }
}

/// <summary>Somebody was added to a conversation and should be told they are in it.</summary>
public sealed class ConversationParticipantAddedConsumer
    : ModuleEventConsumer<ConversationParticipantAddedIntegrationEvent>
{
    public ConversationParticipantAddedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher)
    {
    }

    protected override string SourceModule => "Conversations";

    protected override IEnumerable<NotificationTrigger> Translate(
        ConversationParticipantAddedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.UserId == Guid.Empty)
        {
            yield break;
        }

        var values = Values();
        values["conversationid"] = integrationEvent.ConversationId.ToString("D");
        values["contexttitle"] = integrationEvent.ContextTitle;
        values["contexttype"] = integrationEvent.ContextType;

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "recipient",
            integrationEvent.JoinedAt,
            values,
            subjectType: "conversation",
            subjectId: integrationEvent.ConversationId.ToString("D"));
    }
}

/// <summary>
/// A conversation ended.
/// <para>
/// In-app only, by the rule that routes it. "This listing has been sold" arriving as a phone
/// notification at 2am, for a thread archived last month, is the notification that teaches people to
/// switch notifications off.
/// </para>
/// </summary>
public sealed class ConversationClosedConsumer
    : ModuleEventConsumer<ConversationClosedIntegrationEvent>
{
    public ConversationClosedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher)
    {
    }

    protected override string SourceModule => "Conversations";

    protected override IEnumerable<NotificationTrigger> Translate(
        ConversationClosedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        foreach (var participant in integrationEvent.ParticipantUserIds)
        {
            if (participant == Guid.Empty)
            {
                continue;
            }

            var values = Values();
            values["conversationid"] = integrationEvent.ConversationId.ToString("D");
            values["contexttitle"] = integrationEvent.ContextTitle;
            values["contexttype"] = integrationEvent.ContextType;

            yield return Trigger(
                UserId.From(participant),
                "recipient",
                integrationEvent.ClosedAt,
                values,
                subjectType: "conversation",
                subjectId: integrationEvent.ConversationId.ToString("D"),
                groupKey: integrationEvent.ConversationId.ToString("D"));
        }
    }
}
