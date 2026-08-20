using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// The wording for Community's, Community Life's, Guides', Knowledge's and Providers' messages.
/// <para>
/// <b>None of these name the person who acted.</b> "Somebody replied to you" rather than
/// "Marcus replied to you" — because the published contracts carry an author identifier and not a
/// display name, and resolving one would mean this module querying Identity for a name to put in a
/// push banner. The client resolves the name when the member opens the thread, where it belongs.
/// </para>
/// </summary>
public sealed class CommunityNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityMention,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.High,
            RouteTemplate = "/community/{contentid}",
            Description = "Somebody named a member in a post or a comment.",
            Variables =
            [
                new TemplateVariableSeed { Name = "contentid", Sample = "8f2c" },
                new TemplateVariableSeed { Name = "contenttype", IsRequired = false, Sample = "Post" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You were mentioned",
                    Body = "Somebody mentioned you in a {contenttype}. Open it to reply.",
                    ShortBody = "You were mentioned in a {contenttype}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityReply,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community/{contentid}",
            Description = "Somebody replied to a member's comment.",
            Variables =
            [
                new TemplateVariableSeed { Name = "contentid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "New reply",
                    Body = "Somebody replied to your comment.",
                    ShortBody = "Somebody replied to your comment."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityComment,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Low,
            RouteTemplate = "/community/{contentid}",
            Description = "Somebody commented on a member's content.",
            Variables =
            [
                new TemplateVariableSeed { Name = "contentid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "New comment",
                    Body = "Somebody commented on your post.",
                    PluralBody = "There are new comments on your post.",
                    ShortBody = "New comment on your post."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityReaction,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Low,
            RouteTemplate = "/community/{contentid}",
            Description = "Somebody reacted to a member's content.",
            Variables =
            [
                new TemplateVariableSeed { Name = "contentid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "New reaction",
                    Body = "Somebody reacted to your post.",
                    PluralBody = "People reacted to your post.",
                    ShortBody = "New reaction on your post."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.CommunityModeration,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.High,
            RouteTemplate = "/profile/community-guidelines",
            Description = "A moderator decided something about a member's content.",

            // The moderator is never named. It is a safety problem for them and a policy problem for
            // the platform, and the decision is the platform's rather than an individual's.
            Variables =
            [
                new TemplateVariableSeed { Name = "action", Sample = "Removed" },
                new TemplateVariableSeed { Name = "contenttype", IsRequired = false, Sample = "Post" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "A moderation decision about your content",
                    Body = "Your {contenttype} was {action} under our community guidelines. Open the guidelines to see why and how to appeal.",
                    ShortBody = "Your {contenttype} was {action}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.EventRsvpConfirmed,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/event/{eventid}",
            Description = "A member's response to an event was recorded.",
            Variables =
            [
                new TemplateVariableSeed { Name = "status", Sample = "Going" },
                new TemplateVariableSeed
                {
                    Name = "partysize", Format = TemplateVariable.NumberFormat, Sample = "2"
                },
                new TemplateVariableSeed { Name = "eventid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You are {status}",
                    Body = "Your response has been recorded for {partysize} person.",
                    PluralBody = "Your response has been recorded for {partysize} people.",
                    ShortBody = "You are {status}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.EventRsvpForOrganizer,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.Low,
            RouteTemplate = "/community-life/event/{eventid}",
            Description = "Tells an organizer somebody responded to their event. B13.1.",
            Variables =
            [
                new TemplateVariableSeed { Name = "status", Sample = "Going" },
                new TemplateVariableSeed
                {
                    Name = "partysize", Format = TemplateVariable.NumberFormat, Sample = "2"
                },
                new TemplateVariableSeed
                {
                    Name = "goingcount", Format = TemplateVariable.NumberFormat, Sample = "48"
                },
                new TemplateVariableSeed { Name = "eventid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",

                    // The count, not the name. Who is coming is the most socially sensitive thing
                    // Community Life holds, and the guest list stays on the organizer's screen
                    // rather than travelling into a push notification.
                    Title = "New response to your event",
                    Body = "Somebody is coming. {goingcount} places are now taken.",
                    ShortBody = "New response to your event."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.EventCheckedIn,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.Silent,
            RouteTemplate = "/community-life/event/{eventid}",
            Description = "A member arrived at an event. Recorded, never pushed.",
            Variables =
            [
                new TemplateVariableSeed { Name = "eventid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Checked in",
                    Body = "Your arrival has been recorded. Enjoy the event.",
                    ShortBody = "Checked in."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.OrganizationMembershipApproved,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/org/{organizationid}",
            Description = "A member joined an organization.",
            Variables =
            [
                new TemplateVariableSeed { Name = "role", Sample = "Member" },
                new TemplateVariableSeed { Name = "organizationid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You are now a member",
                    Body = "Your membership has been approved. You joined as {role}.",
                    ShortBody = "Your membership was approved."
                }
            ]
        };

        // ── B13.3 ───────────────────────────────────────────────────────────────────────────
        //
        // Four event contracts the platform published and nothing acted on, and one membership
        // outcome. Every one of them is a fact somebody rearranged their week around; before this
        // they discovered it by opening the app.
        //
        // None of them names who did it. The contracts carry a changer's identifier and not a
        // display name, and resolving one would mean this module asking Identity for a name to put
        // on a lock screen.

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.EventCancelled,
            CategoryKey = NotificationCategoryKeys.Events,

            // High rather than Critical. Critical overrides quiet hours, and quiet hours exist so a
            // member is not woken at 03:00 — a cancellation they will read at breakfast, in time,
            // does not earn that. A cancellation an hour before the doors would, and the platform
            // cannot tell the two apart from this contract alone.
            Priority = NotificationPriority.High,
            RouteTemplate = "/community-life/event/{eventid}",
            Description = "An event somebody was going to was cancelled.",
            Variables =
            [
                new TemplateVariableSeed { Name = "title", Sample = "Lagos Supper Club" },
                new TemplateVariableSeed { Name = "eventid", Sample = "8f2c" },
                new TemplateVariableSeed
                {
                    Name = "startsat", Format = TemplateVariable.DateTimeFormat, IsRequired = false
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{title} was cancelled",
                    Body = "The organizer has cancelled this event. Your place has been released.",
                    ShortBody = "{title} was cancelled."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.EventRescheduled,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.High,
            RouteTemplate = "/community-life/event/{eventid}",
            Description = "An event somebody was going to moved to a different time.",
            Variables =
            [
                new TemplateVariableSeed { Name = "title", Sample = "Lagos Supper Club" },
                new TemplateVariableSeed { Name = "eventid", Sample = "8f2c" },

                // Rendered in the reader's own zone by the renderer, from the instant on the
                // contract. Passing the event's local wall-clock string instead would show somebody
                // in London a time in Lagos and give them no way to know it.
                new TemplateVariableSeed
                {
                    Name = "startsat", Format = TemplateVariable.DateTimeFormat
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{title} has moved",
                    Body = "This event now starts at {startsat}. Your place is unchanged.",
                    ShortBody = "{title} now starts at {startsat}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.EventUpdated,
            CategoryKey = NotificationCategoryKeys.Events,

            // Normal, and only material changes reach it at all — the consumer drops the rest. A
            // member who is told about a corrected typo stops reading the ones about a new venue.
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/event/{eventid}",
            Description = "Something material about an event changed.",
            Variables =
            [
                new TemplateVariableSeed { Name = "title", Sample = "Lagos Supper Club" },
                new TemplateVariableSeed { Name = "eventid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{title} has changed",
                    Body = "The organizer has updated the details. Open it to see what changed.",
                    ShortBody = "{title} has changed."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.EventReminder,
            CategoryKey = NotificationCategoryKeys.Events,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/event/{eventid}",
            Description = "The day before an event a member said they were coming to.",
            Variables =
            [
                new TemplateVariableSeed { Name = "title", Sample = "Lagos Supper Club" },
                new TemplateVariableSeed { Name = "eventid", Sample = "8f2c" },
                new TemplateVariableSeed
                {
                    Name = "startsat", Format = TemplateVariable.DateTimeFormat
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "{title} is tomorrow",
                    Body = "This starts at {startsat}. You said you were going.",
                    ShortBody = "{title} starts at {startsat}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.OrganizationMembershipLapsed,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/org/{organizationid}",

            // Lapsed, not removed. The distinction is the whole message: one is an administrative
            // date passing and is reversible by asking, the other is a decision somebody made about
            // you. Telling a member they were removed when their renewal simply expired is the kind
            // of wording mistake that costs an organization a member.
            Description = "A membership expired because its renewal window passed.",
            Variables =
            [
                new TemplateVariableSeed { Name = "organizationid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your membership has lapsed",
                    Body = "Your membership expired when its renewal window passed. You can ask to "
                        + "rejoin at any time.",
                    ShortBody = "Your membership has lapsed."
                }
            ]
        };
    }
}
