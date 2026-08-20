using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// The wording for B18's messages.
/// <para>
/// <b>None of these names the organisation, and none names the person who decided.</b> "The
/// organisation has accepted your application" rather than "The Nigerian Community Association has":
/// the published contracts carry identifiers and not display names, and resolving one would mean this
/// module querying B12 for a name to put in a push banner. The client resolves it when the member
/// opens the screen, where it belongs.
/// </para>
/// <para>
/// <b>Every route sends the member to a real screen.</b> The frontend's <c>notification-routes.ts</c>
/// already resolves the <c>/community-life</c> prefix, so a membership decision lands where the
/// member expects rather than on a hub they have to navigate from.
/// </para>
/// </summary>
public sealed class CommunityProgrammeTemplateContributor : INotificationTemplateContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProgrammeMembershipApproved,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.High,
            RouteTemplate = "/community-life/membership/{membershipid}",
            Description = "An organisation accepted a membership application.",
            Variables =
            [
                new TemplateVariableSeed { Name = "membershipid", Sample = "8f2c" },
                new TemplateVariableSeed
                {
                    Name = "organizationid", IsRequired = false, Sample = "3a91"
                }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your membership was approved",
                    Body = "The organisation has accepted your application. "
                        + "Open it to see what happens next.",
                    ShortBody = "Your membership application was approved."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProgrammeMembershipRejected,
            CategoryKey = NotificationCategoryKeys.Organizations,

            // Normal rather than high. A refusal is not urgent, and interrupting somebody's evening
            // with one is the opposite of kind.
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/membership/{membershipid}",
            Description = "An organisation refused a membership application.",
            Variables =
            [
                new TemplateVariableSeed { Name = "membershipid", Sample = "8f2c" },

                // The organisation's own words, when it gave any. Optional, because it may not have.
                new TemplateVariableSeed { Name = "reason", IsRequired = false, Sample = "" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your membership application was not accepted",
                    Body = "The organisation did not accept this application. "
                        + "Open it to see what they said.",
                    ShortBody = "Your membership application was not accepted."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProgrammeMembershipRenewalDue,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/membership/{membershipid}",
            Description = "A membership entered its renewal window.",
            Variables = [new TemplateVariableSeed { Name = "membershipid", Sample = "8f2c" }],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your membership needs renewing",
                    Body = "Your membership is due for renewal. "
                        + "It still works in the meantime — renewing keeps it that way.",
                    ShortBody = "Your membership is due for renewal."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.ProgrammeMembershipExpired,
            CategoryKey = NotificationCategoryKeys.Organizations,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/membership/{membershipid}",
            Description = "A membership passed its expiry.",
            Variables = [new TemplateVariableSeed { Name = "membershipid", Sample = "8f2c" }],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your membership has ended",
                    Body = "This membership has ended. You can rejoin whenever you like.",
                    ShortBody = "Your membership has ended."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.VolunteerApplicationDecided,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/community-life/volunteer/applications/{applicationid}",
            Description = "A volunteer application was approved, refused or waitlisted.",
            Variables =
            [
                new TemplateVariableSeed { Name = "applicationid", Sample = "8f2c" },
                new TemplateVariableSeed { Name = "decision", Sample = "Approved" },
                new TemplateVariableSeed { Name = "reason", IsRequired = false, Sample = "" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "There is news about your volunteering",
                    Body = "The organisers have made a decision about your application. "
                        + "Open it to see.",
                    ShortBody = "Your volunteer application was decided."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.VolunteerAssignmentCreated,
            CategoryKey = NotificationCategoryKeys.Community,
            Priority = NotificationPriority.High,
            RouteTemplate = "/community-life/volunteer/assignments/{assignmentid}",
            Description = "An approved volunteer was placed on a shift.",
            Variables = [new TemplateVariableSeed { Name = "assignmentid", Sample = "8f2c" }],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You have been given a shift",
                    Body = "You have been placed on a volunteering shift. "
                        + "Open it to check the time and the place.",
                    ShortBody = "You have been given a volunteering shift."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.VolunteerHoursConfirmed,
            CategoryKey = NotificationCategoryKeys.Community,

            // Low, and digest-bound by rule. A receipt that interrupts somebody who has just walked
            // home from the shift it refers to is the clearest example of a notification nobody asked
            // for — the same reasoning B13 applies to an event check-in.
            Priority = NotificationPriority.Low,
            RouteTemplate = "/community-life/volunteer/hours",
            Description = "An organiser confirmed the hours a volunteer worked.",
            Variables =
            [
                new TemplateVariableSeed { Name = "assignmentid", Sample = "8f2c" },
                new TemplateVariableSeed { Name = "hours", Sample = "3.5" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your volunteering hours were confirmed",
                    Body = "An organiser confirmed {hours} hours. They now count towards your record.",
                    ShortBody = "{hours} volunteering hours were confirmed."
                }
            ]
        };
    }
}
