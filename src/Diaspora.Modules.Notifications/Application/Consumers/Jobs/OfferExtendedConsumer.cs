using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Jobs;

/// <summary>
/// An employer made an offer.
/// <para>
/// The salary travels as an amount and a currency code separately, so the template's currency
/// formatter can render it in the reader's own grouping while keeping the employer's currency. A
/// pre-formatted string here would be an offer denominated in whatever the publisher's culture
/// happened to be.
/// </para>
/// <para>
/// The expiry is carried so the reminder rule has a moment to schedule against: an offer nobody
/// answered because they did not see it is the worst possible outcome of this module failing.
/// </para>
/// </summary>
public sealed class OfferExtendedConsumer : ModuleEventConsumer<OfferExtendedIntegrationEvent>
{
    public OfferExtendedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Jobs";

    protected override IEnumerable<NotificationTrigger> Translate(
        OfferExtendedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["jobTitle"] = integrationEvent.JobTitle;
        values["employerName"] = integrationEvent.EmployerName;
        values["salary"] = $"{integrationEvent.SalaryAmount} {integrationEvent.CurrencyCode}";
        values["salaryPeriod"] = integrationEvent.SalaryPeriod;
        values["expiresAt"] = integrationEvent.ExpiresAt;
        values["jobId"] = integrationEvent.JobId;
        values["applicationId"] = integrationEvent.ApplicationId;

        // See InterviewScheduledConsumer: events published before B13.1 carry the empty identifier.
        if (integrationEvent.CandidateUserId == Guid.Empty)
        {
            yield break;
        }

        var candidate = UserId.From(integrationEvent.CandidateUserId);

        yield return Trigger(
            candidate,
            "candidate",
            integrationEvent.ExtendedAt,
            values,
            subjectType: "offer",
            subjectId: integrationEvent.OfferId.ToString(),
            subjectMoment: integrationEvent.ExpiresAt);

        yield return Trigger(
            candidate,
            "candidate-reminder",
            integrationEvent.ExtendedAt,
            values,
            subjectType: "offer",
            subjectId: integrationEvent.OfferId.ToString(),
            subjectMoment: integrationEvent.ExpiresAt);
    }
}
