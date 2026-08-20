using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Routing;

namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// How Community's and Community Life's published facts become messages.
/// <para>
/// <b>This file is where the brief's digest examples actually live.</b> Reactions are Low and
/// digest-bound; mentions are High and immediate; a moderation decision is High, in a mandatory
/// category, and cannot be silenced at all. The difference between somebody enjoying the platform
/// and switching notifications off entirely is almost entirely in these four rows.
/// </para>
/// </summary>
public sealed class CommunityNotificationRuleContributor : INotificationRuleContributor
{
    private static readonly NotificationChannelKind[] InAppAndPush =
        [NotificationChannelKind.InApp, NotificationChannelKind.Push];

    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        yield return new NotificationRuleSeed
        {
            Key = "community.mention",
            EventName = typeof(CommunityMentionCreatedIntegrationEvent).FullName!,
            Audience = "mentioned",
            TemplateKey = NotificationTemplateKeys.CommunityMention,
            CategoryKey = NotificationCategoryKeys.Community,

            // The one community signal that is genuinely urgent: a mention is somebody asking you a
            // question in public.
            Priority = NotificationPriority.High,
            Channels = InAppAndPush
        };

        yield return new NotificationRuleSeed
        {
            Key = "community.reply",
            EventName = typeof(CommunityCommentAddedIntegrationEvent).FullName!,
            Audience = "comment-parent-author",
            TemplateKey = NotificationTemplateKeys.CommunityReply,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush,
            DeduplicationWindow = TimeSpan.FromMinutes(10)
        };

        yield return new NotificationRuleSeed
        {
            Key = "community.comment",
            EventName = typeof(CommunityCommentAddedIntegrationEvent).FullName!,
            Audience = "comment-content-author",
            TemplateKey = NotificationTemplateKeys.CommunityComment,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Low,
            Policy = DeliveryPolicy.Digest,
            Channels = [NotificationChannelKind.InApp]
        };

        yield return new NotificationRuleSeed
        {
            Key = "community.reaction",
            EventName = typeof(CommunityReactionAddedIntegrationEvent).FullName!,
            Audience = "content-author",
            TemplateKey = NotificationTemplateKeys.CommunityReaction,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Low,
            Policy = DeliveryPolicy.Digest,
            Channels = [NotificationChannelKind.InApp],

            // An hour. Twelve people reacting to one post in an evening is one digest line.
            DeduplicationWindow = TimeSpan.FromHours(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "moderation.decision",
            EventName = typeof(CommunityModerationDecidedIntegrationEvent).FullName!,
            Audience = "content-author",
            TemplateKey = NotificationTemplateKeys.CommunityModeration,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.High,
            Channels = InAppAndPush,
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = "events.rsvp.confirmed",
            EventName = typeof(RsvpSubmittedIntegrationEvent).FullName!,
            Audience = "attendee",
            TemplateKey = NotificationTemplateKeys.EventRsvpConfirmed,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.Normal,
            Channels = [NotificationChannelKind.InApp]
        };

        // B13.1. The organizer side of the same response, addressable for the first time. Digested
        // and de-duplicated by the hour on purpose: an event that fills in an afternoon produces
        // one fact per place taken, and an organizer interrupted three hundred times has been
        // punished for running something popular.
        yield return new NotificationRuleSeed
        {
            Key = "events.rsvp.received",
            EventName = typeof(RsvpSubmittedIntegrationEvent).FullName!,
            Audience = "organizer",
            TemplateKey = NotificationTemplateKeys.EventRsvpForOrganizer,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.Low,
            Policy = DeliveryPolicy.Digest,
            Channels = [NotificationChannelKind.InApp],
            DeduplicationWindow = TimeSpan.FromHours(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "events.checkin.recorded",
            EventName = typeof(EventCheckInCompletedIntegrationEvent).FullName!,
            Audience = "attendee",
            TemplateKey = NotificationTemplateKeys.EventCheckedIn,
            CategoryKey = NotificationCategoryKeys.Events,

            // Silent: a receipt that interrupts somebody standing in the room it refers to is the
            // clearest possible example of a notification nobody asked for. It is still recorded.
            Priority = NotificationPriority.Silent,
            Channels = [NotificationChannelKind.InApp]
        };

        yield return new NotificationRuleSeed
        {
            Key = "organizations.membership.approved",
            EventName = typeof(OrganizationMemberJoinedIntegrationEvent).FullName!,
            Audience = "member",
            TemplateKey = NotificationTemplateKeys.OrganizationMembershipApproved,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush
        };

        // ── B13.3 ───────────────────────────────────────────────────────────────────────────

        yield return new NotificationRuleSeed
        {
            Key = "events.cancelled",
            EventName = typeof(EventCancelledIntegrationEvent).FullName!,
            Audience = "attendee",
            TemplateKey = NotificationTemplateKeys.EventCancelled,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.High,
            Channels = InAppAndPush,

            // No de-duplication window. An event is cancelled once, and the consumer resolves the
            // attendee list per delivery — a second delivery of the same outbox row produces the
            // same recipients, which the dispatcher's own source-keyed check already collapses.
            // Setting a window here as well would silence a genuine re-cancellation of a restored
            // event, which is rare and is exactly when somebody most needs telling.
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = "events.rescheduled",
            EventName = typeof(EventOccurrenceRescheduledIntegrationEvent).FullName!,
            Audience = "attendee",
            TemplateKey = NotificationTemplateKeys.EventRescheduled,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.High,
            Channels = InAppAndPush,
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = "events.updated",
            EventName = typeof(EventUpdatedIntegrationEvent).FullName!,
            Audience = "attendee",
            TemplateKey = NotificationTemplateKeys.EventUpdated,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush,

            // An organizer correcting three things in a row is one message. The consumer already
            // drops immaterial changes; this stops the material ones arriving as a stream.
            DeduplicationWindow = TimeSpan.FromHours(1)
        };

        // The scheduled one, and the only rule in this file that does not send when it matches.
        //
        // The offset is applied to the moment the event names — EventStartsAt, which the RSVP
        // contract carries since B13.3 — exactly as jobs.interview.reminder and
        // housing.viewing.reminder have worked since B13. A response made after the offset has
        // already passed sends immediately rather than being skipped: somebody who says they are
        // coming at nine in the evening for a nine-in-the-morning event still deserves reminding.
        yield return new NotificationRuleSeed
        {
            Key = "events.reminder",
            EventName = typeof(RsvpSubmittedIntegrationEvent).FullName!,

            // Its own audience rather than a second rule on "attendee". The consumer yields this
            // trigger only when the contract carries a start and a title, so a response published
            // before the B13.3 enrichment produces a confirmation and no reminder — rather than a
            // reminder that fires immediately against a missing moment.
            Audience = "attendee-reminder",
            TemplateKey = NotificationTemplateKeys.EventReminder,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Scheduled,
            Offset = TimeSpan.FromHours(-24),
            Channels = InAppAndPush
        };

        yield return new NotificationRuleSeed
        {
            Key = "organizations.membership.lapsed",
            EventName = typeof(OrganizationMemberLeftIntegrationEvent).FullName!,
            Audience = "lapsed-member",
            TemplateKey = NotificationTemplateKeys.OrganizationMembershipLapsed,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush
        };
    }
}
