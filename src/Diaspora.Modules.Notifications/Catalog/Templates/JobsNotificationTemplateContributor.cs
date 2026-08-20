using Diaspora.Modules.Notifications.Catalog.Rules;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;

namespace Diaspora.Modules.Notifications.Catalog.Templates;

/// <summary>
/// The wording for Jobs' messages.
/// <para>
/// <b>The interview templates are where the localization machinery earns its keep.</b>
/// <c>scheduledat</c> is declared with the <c>datetime</c> format, so the renderer converts it into
/// the candidate's own time zone and culture — and the employer's zone travels as a separate value
/// so the message can name both clocks. A candidate in Lagos reading an interview time in Berlin's
/// wall clock is the worst bug this module could ship, and it is one missing format hint away.
/// </para>
/// </summary>
public sealed class JobsNotificationTemplateContributor : INotificationTemplateContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationTemplateSeed> GetTemplates()
    {
        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.JobApplicationReceived,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/jobs/{jobid}",
            Description = "Acknowledges an application to the candidate.",
            Variables =
            [
                new TemplateVariableSeed { Name = "jobid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Application received",
                    Body = "Your application has been sent. You will hear here as soon as the employer responds.",
                    ShortBody = "Your application has been sent."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.JobApplicationForEmployer,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.Normal,
            RouteTemplate = "/jobs/{jobid}/applications",
            Description = "Tells an employer an application is waiting. B13.1.",
            Variables =
            [
                new TemplateVariableSeed { Name = "jobid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",

                    // No candidate name and no candidate detail. Jobs keeps an applicant's
                    // profile off the published contract, and a notification is a prompt to open
                    // the screen where that information properly lives.
                    Title = "New application",
                    Body = "You have a new application waiting for review.",
                    ShortBody = "New application received."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.JobShortlisted,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.High,
            RouteTemplate = "/jobs/{jobid}",
            Description = "A candidate was shortlisted.",
            Variables =
            [
                new TemplateVariableSeed { Name = "jobtitle", Sample = "Senior React Native Developer" },
                new TemplateVariableSeed { Name = "employername", Sample = "Kite Labs" },
                new TemplateVariableSeed { Name = "jobid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You have been shortlisted",
                    Body = "{employername} has shortlisted you for {jobtitle}.",
                    ShortBody = "Shortlisted for {jobtitle}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.JobInterviewScheduled,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.High,
            RouteTemplate = "/jobs/{jobid}",
            Description = "An interview was scheduled or moved.",
            Variables =
            [
                new TemplateVariableSeed { Name = "jobtitle", Sample = "Senior React Native Developer" },
                new TemplateVariableSeed { Name = "employername", Sample = "Kite Labs" },
                new TemplateVariableSeed
                {
                    Name = "scheduledat",
                    Format = TemplateVariable.DateTimeFormat,
                    Sample = "Thursday 14 March at 14:00"
                },
                new TemplateVariableSeed { Name = "mode", Sample = "Video" },
                new TemplateVariableSeed { Name = "jobid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Interview confirmed",
                    Body = "Your {mode} interview with {employername} for {jobtitle} is on {scheduledat}, your local time.",
                    ShortBody = "Interview with {employername} on {scheduledat}."
                },
                new TemplateLocalizationSeed
                {
                    Language = "fr",
                    Title = "Entretien confirmé",
                    Body = "Votre entretien {mode} avec {employername} pour {jobtitle} aura lieu le {scheduledat}, à votre heure locale.",
                    ShortBody = "Entretien avec {employername} le {scheduledat}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.JobInterviewReminder,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.Critical,
            RouteTemplate = "/jobs/{jobid}",
            Description = "Sent a day before an interview.",

            // Two days. A reminder about an interview that has happened is worse than none, so the
            // retention sweep clears it rather than leaving it in the inbox to confuse somebody.
            RetentionPeriod = null,
            Variables =
            [
                new TemplateVariableSeed { Name = "jobtitle", Sample = "Senior React Native Developer" },
                new TemplateVariableSeed { Name = "employername", Sample = "Kite Labs" },
                new TemplateVariableSeed
                {
                    Name = "scheduledat",
                    Format = TemplateVariable.DateTimeFormat,
                    Sample = "Thursday 14 March at 14:00"
                },
                new TemplateVariableSeed { Name = "jobid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Interview tomorrow",
                    Body = "A reminder: your interview with {employername} for {jobtitle} is on {scheduledat}, your local time.",
                    ShortBody = "Interview tomorrow with {employername}."
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.JobOfferExtended,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.High,
            RouteTemplate = "/jobs/{jobid}",
            Description = "An employer made an offer.",
            Variables =
            [
                new TemplateVariableSeed { Name = "jobtitle", Sample = "Senior React Native Developer" },
                new TemplateVariableSeed { Name = "employername", Sample = "Kite Labs" },
                new TemplateVariableSeed
                {
                    Name = "salary", Format = TemplateVariable.CurrencyFormat, Sample = "62000 GBP"
                },
                new TemplateVariableSeed { Name = "salaryperiod", Sample = "Yearly" },
                new TemplateVariableSeed
                {
                    Name = "expiresat", Format = TemplateVariable.DateFormat, Sample = "21 March"
                },
                new TemplateVariableSeed { Name = "jobid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "You have an offer",
                    Body = "{employername} has offered you {jobtitle} at {salary} {salaryperiod}. It is open until {expiresat}.",
                    ShortBody = "{employername} offered you {jobtitle}."
                }
            ],
            Actions =
            [
                new TemplateActionSeed
                {
                    Key = "view", Label = "View offer", RouteTemplate = "/jobs/{jobid}"
                }
            ]
        };

        yield return new NotificationTemplateSeed
        {
            Key = NotificationTemplateKeys.JobOfferReminder,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.High,
            RouteTemplate = "/jobs/{jobid}",
            Description = "Sent two days before an offer lapses.",
            Variables =
            [
                new TemplateVariableSeed { Name = "employername", Sample = "Kite Labs" },
                new TemplateVariableSeed
                {
                    Name = "expiresat", Format = TemplateVariable.DateFormat, Sample = "21 March"
                },
                new TemplateVariableSeed { Name = "jobid", Sample = "8f2c" }
            ],
            Localizations =
            [
                new TemplateLocalizationSeed
                {
                    Language = "en",
                    Title = "Your offer expires soon",
                    Body = "The offer from {employername} is open until {expiresat}. Answer before then to keep it.",
                    ShortBody = "Your offer from {employername} expires {expiresat}."
                }
            ]
        };
    }
}
