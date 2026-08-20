using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Routing;

namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// How Jobs' published facts become messages.
/// <para>
/// <b>Two rules per interview and per offer, and the second one is the point.</b> The confirmation
/// goes now; the reminder is scheduled against the moment itself with a negative offset, so "the day
/// before" is a value in a row rather than logic in a handler. A candidate who misses an interview
/// has lost the job, and a cross-border candidate may have booked a flight against it.
/// </para>
/// </summary>
public sealed class JobsNotificationRuleContributor : INotificationRuleContributor
{
    private static readonly NotificationChannelKind[] Everywhere =
        [NotificationChannelKind.InApp, NotificationChannelKind.Push, NotificationChannelKind.Email];

    private static readonly NotificationChannelKind[] InAppAndPush =
        [NotificationChannelKind.InApp, NotificationChannelKind.Push];

    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        yield return new NotificationRuleSeed
        {
            Key = "jobs.application.received",
            EventName = typeof(ApplicationSubmittedIntegrationEvent).FullName!,
            Audience = "candidate",
            TemplateKey = NotificationTemplateKeys.JobApplicationReceived,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.Normal,
            Channels = [NotificationChannelKind.InApp]
        };

        // B13.1. The employer side of the same fact, addressable for the first time because the
        // contract now names a member at the employer. Digest-bound and de-duplicated by the hour:
        // a posting that attracts forty applications in an afternoon must not produce forty
        // interruptions, and the recruiter's screen is where the applications actually live.
        yield return new NotificationRuleSeed
        {
            Key = "jobs.application.new",
            EventName = typeof(ApplicationSubmittedIntegrationEvent).FullName!,
            Audience = "employer",
            TemplateKey = NotificationTemplateKeys.JobApplicationForEmployer,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.Normal,
            Policy = DeliveryPolicy.Digest,
            Channels = [NotificationChannelKind.InApp],
            DeduplicationWindow = TimeSpan.FromHours(1)
        };

        yield return new NotificationRuleSeed
        {
            Key = "jobs.application.shortlisted",
            EventName = typeof(CandidateShortlistedIntegrationEvent).FullName!,
            Audience = "candidate",
            TemplateKey = NotificationTemplateKeys.JobShortlisted,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.High,
            Channels = InAppAndPush
        };

        yield return new NotificationRuleSeed
        {
            Key = "jobs.interview.scheduled",
            EventName = typeof(InterviewScheduledIntegrationEvent).FullName!,
            Audience = "candidate",
            TemplateKey = NotificationTemplateKeys.JobInterviewScheduled,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.High,
            Channels = Everywhere,

            // No window. A reschedule minutes after a booking is exactly the pair of messages
            // somebody needs both of.
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = "jobs.interview.reminder",
            EventName = typeof(InterviewScheduledIntegrationEvent).FullName!,
            Audience = "candidate-reminder",
            TemplateKey = NotificationTemplateKeys.JobInterviewReminder,
            CategoryKey = NotificationCategoryKeys.Jobs,

            // Critical: it bypasses quiet hours, because an interview at 08:00 needs a reminder at
            // 08:00 the day before and a member's quiet hours commonly cover it.
            Priority = NotificationPriority.Critical,
            Policy = DeliveryPolicy.Scheduled,
            Offset = TimeSpan.FromHours(-24),
            Channels = Everywhere,
            DeduplicationWindow = null
        };

        yield return new NotificationRuleSeed
        {
            Key = "jobs.offer.extended",
            EventName = typeof(OfferExtendedIntegrationEvent).FullName!,
            Audience = "candidate",
            TemplateKey = NotificationTemplateKeys.JobOfferExtended,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.High,
            Channels = Everywhere
        };

        yield return new NotificationRuleSeed
        {
            Key = "jobs.offer.reminder",
            EventName = typeof(OfferExtendedIntegrationEvent).FullName!,
            Audience = "candidate-reminder",
            TemplateKey = NotificationTemplateKeys.JobOfferReminder,
            CategoryKey = NotificationCategoryKeys.Jobs,
            Priority = NotificationPriority.High,
            Policy = DeliveryPolicy.Scheduled,

            // Two days before it lapses. An offer nobody answered because they did not see it is the
            // worst possible outcome of this module failing.
            Offset = TimeSpan.FromHours(-48),
            Channels = Everywhere
        };
    }
}
