using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// What a member reads when staff act on their content, and what a reporter reads (7 Nov 2026).
/// <para>
/// <b>Everything is in the body.</b> Email variants are never rendered and template actions are shown
/// nowhere, so the reason, the moderator's note and what to do next all have to be in the text. The
/// moderator is never named: the decision is Diaspora's, not one person's, and naming them is a
/// safety problem for them.
/// </para>
/// <para>
/// The route opens the decision in the app, where the owner can ask for a review or appeal.
/// </para>
/// </summary>
public sealed class ModerationCaseNotificationTemplateContributor : INotificationTemplateContributor
{
    private const string CaseRoute = "/profile/moderation/{caseid}";

    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ModerationCaseHidden,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            RouteTemplate = CaseRoute,
            Description = "Staff hid a member's content pending review, with the reason and the note.",
            Variables = OwnerVariables(),
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your {contenttype} is hidden while Diaspora reviews it",
                    Body = "\"{title}\" has been taken out of public view because it was found to be {reasonlabel}. "
                        + "Diaspora's note: {note} If you can put it right, edit it and then ask for a review "
                        + "from this notice. If you think the decision is wrong, you can appeal from Profile, Appeals.",
                    ShortBody = "\"{title}\" is hidden: it was found to be {reasonlabel}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ModerationCaseHiddenAutomatically,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            RouteTemplate = CaseRoute,
            Description = "Many members reported a member's content for a serious reason, so it was hidden before anyone at Diaspora had looked.",
            Variables = OwnerVariables(),
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your {contenttype} is hidden after several reports",
                    Body = "\"{title}\" has been taken out of public view because several members reported it as "
                        + "{reasonlabel}. Nobody at Diaspora has decided anything yet. A member of staff will look "
                        + "at it soon, and you will be told what they decide.",
                    ShortBody = "\"{title}\" is hidden after several reports, until staff review it."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ModerationCaseRemoved,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            RouteTemplate = CaseRoute,
            Description = "Two members of staff agreed to remove a member's content; the owner may appeal.",
            Variables =
            [
                .. OwnerVariables(),
                new TemplateVariableSeed { Name = "appealdeadline", Sample = "21 November 2026" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your {contenttype} was removed",
                    Body = "\"{title}\" was removed from Diaspora because it was found to be {reasonlabel}. "
                        + "Diaspora's note: {note} Two members of staff agreed on this. You can appeal until "
                        + "{appealdeadline} from Profile, Appeals, and nothing else happens to your account before then.",
                    ShortBody = "\"{title}\" was removed: it was found to be {reasonlabel}. You can appeal."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ModerationCaseRestored,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.High,
            RouteTemplate = CaseRoute,
            Description = "A member's content was put back after a moderation decision.",
            Variables = OwnerVariables(),
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your {contenttype} is back",
                    Body = "\"{title}\" is public again. Diaspora's note: {note}",
                    ShortBody = "\"{title}\" is public again."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ModerationReportOutcome,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.High,
            RouteTemplate = "/notifications",
            Description = "What came of a member's report: action taken or no action, without detail.",
            Variables =
            [
                new TemplateVariableSeed { Name = "contenttype", Sample = "job advert" },
                new TemplateVariableSeed { Name = "outcometext", Sample = "took action" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "About the {contenttype} you reported",
                    Body = "Thank you for reporting it. Diaspora looked at it and {outcometext}.",
                    ShortBody = "Diaspora looked at what you reported and {outcometext}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ModerationCaseStood,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            RouteTemplate = "/profile/moderation/{caseid}",
            Description = "A removal of a member's content stood: the warning or strike and the points it cost.",
            Variables =
            [
                new TemplateVariableSeed { Name = "title", Sample = "Care Assistant" },
                new TemplateVariableSeed { Name = "standstext", Sample = "The time to appeal has passed, so the removal stands." },
                new TemplateVariableSeed { Name = "striketext", IsRequired = false, Sample = "This is a warning." },
                new TemplateVariableSeed { Name = "pointstext", IsRequired = false, Sample = "Your reputation went down by 25 points." },
                new TemplateVariableSeed { Name = "caseid", Sample = "9d1f9a6e-0000-0000-0000-000000000000" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "What the removal of \"{title}\" means for you",
                    Body = "{standstext} {striketext} {pointstext}",
                    ShortBody = "The removal of \"{title}\" stands."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.AppealUpheld,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            RouteTemplate = "/profile/appeals",
            Description = "A member's appeal was upheld, with the reviewer's explanation.",
            Variables =
            [
                new TemplateVariableSeed { Name = "appealtype", Sample = "content" },
                new TemplateVariableSeed { Name = "explanationtext", IsRequired = false, Sample = "The reviewer's explanation: the advert was genuine. " }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your appeal was upheld",
                    Body = "A member of Diaspora staff who had no part in the original decision looked at your "
                        + "{appealtype} appeal and upheld it. {explanationtext}What was decided against you has been undone.",
                    ShortBody = "Your {appealtype} appeal was upheld."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.AppealRefused,
            CategoryKey = NotificationCategoryKeys.Moderation,
            Priority = NotificationPriority.Critical,
            RouteTemplate = "/profile/appeals",
            Description = "A member's appeal was not upheld, with the reviewer's explanation.",
            Variables =
            [
                new TemplateVariableSeed { Name = "appealtype", Sample = "content" },
                new TemplateVariableSeed { Name = "explanationtext", IsRequired = false, Sample = "The reviewer's explanation: the fee demand is in the second paragraph. " }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your appeal was not upheld",
                    Body = "A member of Diaspora staff who had no part in the original decision looked at your "
                        + "{appealtype} appeal and did not uphold it. {explanationtext}The decision stands.",
                    ShortBody = "Your {appealtype} appeal was not upheld."
                }
            ]
        };
    }

    private static TemplateVariableSeed[] OwnerVariables() =>
    [
        new TemplateVariableSeed { Name = "contenttype", Sample = "job advert" },
        new TemplateVariableSeed { Name = "title", Sample = "Care Assistant" },
        new TemplateVariableSeed { Name = "reasonlabel", Sample = "asking people to pay up front" },
        new TemplateVariableSeed { Name = "note", Sample = "The advert asks candidates for a visa fee." },
        new TemplateVariableSeed { Name = "caseid", Sample = "9d1f9a6e-0000-0000-0000-000000000000" }
    ];
}
