using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Trust;

/// <summary>
/// Trust awarded a badge.
/// <para>
/// Low urgency by rule, and it should be: a badge is good news that keeps. It is grouped on the
/// badge so a member who earns three in one afternoon reads one digest line rather than three
/// interruptions.
/// </para>
/// </summary>
public sealed class BadgeAwardedConsumer : ModuleEventConsumer<BadgeAwardedIntegrationEvent>
{
    public BadgeAwardedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Trust";

    protected override IEnumerable<NotificationTrigger> Translate(
        BadgeAwardedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["badge"] = integrationEvent.Badge;
        values["countryCode"] = integrationEvent.CountryCode ?? string.Empty;
        values["awardedAt"] = integrationEvent.AwardedAt;

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.AwardedAt,
            values,
            subjectType: "badge",
            subjectId: integrationEvent.Badge,
            groupKey: "trust:badges");
    }
}
