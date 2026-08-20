using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Catalog;
using Diaspora.Modules.Notifications.Extensibility.Routing;
using Diaspora.Modules.Notifications.Extensibility.Templates;

// Aliased, and it has to be. Inside Diaspora.Modules.Conversations.*, the identifier `Contracts`
// binds to this module's own Contracts namespace before it ever reaches Diaspora.Contracts — so
// `Contracts.Conversations.V1.Events` resolves to something that does not exist, and the error names
// the wrong namespace. A `global::` prefix would work too; an alias says why once instead of at
// every use.
using ConversationEvents = Diaspora.Contracts.Conversations.V1.Events;

namespace Diaspora.Modules.Conversations.Catalog;

/// <summary>The keys this module contributes to B13's catalogues.</summary>
public static class ConversationNotificationKeys
{
    public const string Category = "messages";

    public const string NewMessageTemplate = "conversation.message.new";
    public const string MentionTemplate = "conversation.message.mention";
    public const string ReplyTemplate = "conversation.message.reply";
    public const string InviteTemplate = "conversation.invite";
    public const string ClosedTemplate = "conversation.closed";

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Rule event names are the integration event's full type name, because that is what
    // ModuleEventConsumer stamps on the trigger it builds — `typeof(TIntegrationEvent).FullName`.
    // A friendly string here would produce a rule nothing ever matches, and the symptom would be
    // silence rather than an error: the notification simply never appears.
    //
    // The reply case is not a second event. One message-sent event carries RepliedToUserId, and the
    // consumer picks the reply template for that one recipient — so the rule key is suffixed while
    // the event name is the same type.
    // ────────────────────────────────────────────────────────────────────────────────────────────

    public static readonly string NewMessageEvent =
        typeof(ConversationEvents.ConversationMessageSentIntegrationEvent).FullName!;

    public static readonly string InviteEvent =
        typeof(ConversationEvents.ConversationParticipantAddedIntegrationEvent).FullName!;

    public static readonly string ClosedEvent =
        typeof(ConversationEvents.ConversationClosedIntegrationEvent).FullName!;
}

/// <summary>
/// The messages category.
/// <para>
/// <b>Not mandatory, and that is the deliberate difference from the security and financial
/// categories.</b> A member who switches off message notifications has made a legitimate choice
/// about their own attention; a member who could switch off "your password changed" has made a
/// choice an attacker would make for them. Messages are also the category most likely to be muted
/// per-conversation, which is a finer control the member already has.
/// </para>
/// <para>
/// <b>Digest is off by default.</b> A message batched into tomorrow's summary is a message that
/// arrives after the listing sold. High priority for the same reason: a conversation is a person
/// waiting.
/// </para>
/// </summary>
public sealed class ConversationNotificationCategoryContributor : INotificationCategoryContributor
{
    public string Module => ConversationsModuleMetadata.Name;

    public IEnumerable<NotificationCategorySeed> GetCategories()
    {
        yield return new NotificationCategorySeed
        {
            Key = ConversationNotificationKeys.Category,
            DisplayName = "Messages",
            Description = "New messages, replies and conversation invitations.",
            DefaultPriority = NotificationPriority.High,
            IsMandatory = false,
            DigestByDefault = false,
            Position = 5
        };
    }
}

/// <summary>
/// How a conversation fact becomes a notification.
/// <para>
/// <b>Every rule deduplicates on a window, and the window is the whole design.</b> Somebody typing
/// four messages in a row is one conversation, not four interruptions — B13's grouping collapses
/// them on the conversation identifier, so the member gets one notification that updates. Without a
/// window, an animated back-and-forth would be a phone buzzing thirty times.
/// </para>
/// <para>
/// A reply gets its own rule and its own shorter window: being answered directly is a stronger
/// signal than a message arriving, and collapsing it into the general one would lose that.
/// </para>
/// </summary>
public sealed class ConversationNotificationRuleContributor : INotificationRuleContributor
{
    public string Module => ConversationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        yield return new NotificationRuleSeed
        {
            Key = "conversation.message.new",
            EventName = ConversationNotificationKeys.NewMessageEvent,
            TemplateKey = ConversationNotificationKeys.NewMessageTemplate,
            CategoryKey = ConversationNotificationKeys.Category,
            Priority = NotificationPriority.High,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push],

            // Five minutes. Long enough to collapse a burst, short enough that a reply half an hour
            // later still reaches somebody who has put their phone down.
            DeduplicationWindow = TimeSpan.FromMinutes(5)
        };

        // Same event, different audience. B13 resolves a rule by (event name, audience), so the
        // consumer sends "recipient" for everybody and "replied" for the one person answered.
        yield return new NotificationRuleSeed
        {
            Key = "conversation.message.reply",
            EventName = ConversationNotificationKeys.NewMessageEvent,
            Audience = "replied",
            TemplateKey = ConversationNotificationKeys.ReplyTemplate,
            CategoryKey = ConversationNotificationKeys.Category,
            Priority = NotificationPriority.High,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push],
            DeduplicationWindow = TimeSpan.FromMinutes(2)
        };

        yield return new NotificationRuleSeed
        {
            Key = "conversation.invite",
            EventName = ConversationNotificationKeys.InviteEvent,
            TemplateKey = ConversationNotificationKeys.InviteTemplate,
            CategoryKey = ConversationNotificationKeys.Category,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp, NotificationChannelKind.Push]
        };

        // In-app only. A conversation ending is context somebody wants when they next look, not a
        // reason to light up a lock screen — and "this listing has been sold" arriving at 2am for a
        // thread archived last month is the notification that teaches people to switch notifications
        // off.
        yield return new NotificationRuleSeed
        {
            Key = "conversation.closed",
            EventName = ConversationNotificationKeys.ClosedEvent,
            TemplateKey = ConversationNotificationKeys.ClosedTemplate,
            CategoryKey = ConversationNotificationKeys.Category,
            Priority = NotificationPriority.Low,
            Policy = DeliveryPolicy.Immediate,
            Channels = [NotificationChannelKind.InApp]
        };
    }
}

/// <summary>
/// The wording.
/// <para>
/// <b>No message body appears in any of these.</b> A notification's body is rendered on a lock
/// screen, which is the one surface with no authentication in front of it — putting what somebody
/// wrote there would show a private conversation to anybody who picked up the phone. The templates
/// say that a message arrived and where, and the member opens the app to read it.
/// </para>
/// <para>
/// No sender name either, and for a second reason: this module resolves none. The variables are
/// filled from the trigger, and the trigger carries what a conversation knows about itself.
/// </para>
/// </summary>
public sealed class ConversationNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => ConversationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = ConversationNotificationKeys.NewMessageTemplate,
            CategoryKey = ConversationNotificationKeys.Category,
            Priority = NotificationPriority.High,
            RouteTemplate = "/conversation/{conversationid}",
            Description = "A message arrived in a conversation.",
            Variables =
            [
                new TemplateVariableSeed { Name = "conversationid", Sample = "conv-001" },
                new TemplateVariableSeed
                {
                    Name = "contexttitle", IsRequired = false, Sample = "Toyota Corolla 2018"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "New message",
                    Body = "You have a new message about {contexttitle}.",
                    ShortBody = "New message"
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Nouveau message",
                    Body = "Vous avez un nouveau message à propos de {contexttitle}.",
                    ShortBody = "Nouveau message"
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = ConversationNotificationKeys.ReplyTemplate,
            CategoryKey = ConversationNotificationKeys.Category,
            Priority = NotificationPriority.High,
            RouteTemplate = "/conversation/{conversationid}",
            Description = "Somebody replied to a message this member sent.",
            Variables =
            [
                new TemplateVariableSeed { Name = "conversationid", Sample = "conv-001" },
                new TemplateVariableSeed
                {
                    Name = "contexttitle", IsRequired = false, Sample = "Toyota Corolla 2018"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "New reply",
                    Body = "Someone replied to you about {contexttitle}.",
                    ShortBody = "New reply"
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Nouvelle réponse",
                    Body = "Quelqu'un vous a répondu à propos de {contexttitle}.",
                    ShortBody = "Nouvelle réponse"
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = ConversationNotificationKeys.InviteTemplate,
            CategoryKey = ConversationNotificationKeys.Category,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/conversation/{conversationid}",
            Description = "This member was added to a conversation.",
            Variables =
            [
                new TemplateVariableSeed { Name = "conversationid", Sample = "conv-001" },
                new TemplateVariableSeed
                {
                    Name = "contexttitle", IsRequired = false, Sample = "Toyota Corolla 2018"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You were added to a conversation",
                    Body = "You have been added to a conversation about {contexttitle}.",
                    ShortBody = "Added to a conversation"
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Vous avez été ajouté à une conversation",
                    Body = "Vous avez été ajouté à une conversation à propos de {contexttitle}.",
                    ShortBody = "Ajouté à une conversation"
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = ConversationNotificationKeys.ClosedTemplate,
            CategoryKey = ConversationNotificationKeys.Category,
            Priority = NotificationPriority.Low,
            RouteTemplate = "/conversation/{conversationid}",
            Description = "A conversation ended because the thing it was about did.",
            Variables =
            [
                new TemplateVariableSeed { Name = "conversationid", Sample = "conv-001" },
                new TemplateVariableSeed
                {
                    Name = "contexttitle", IsRequired = false, Sample = "Toyota Corolla 2018"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Conversation closed",
                    Body = "Your conversation about {contexttitle} has been closed.",
                    ShortBody = "Conversation closed"
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Conversation fermée",
                    Body = "Votre conversation à propos de {contexttitle} a été fermée.",
                    ShortBody = "Conversation fermée"
                }
            ]
        };
    }
}
