using Diaspora.Contracts.Intelligence.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Modules.Notifications.Extensibility.Audiences;

namespace Diaspora.Modules.Notifications.Application.Consumers.Intelligence;

/// <summary>
/// An official notice went live; tell the members living in its country.
/// <para>
/// Shaped like <c>KnowledgeGuideFeaturedConsumer</c>, the other broadcast: the event names a country
/// and no person, and <see cref="INotificationAudienceResolver"/> answers who lives there. Bounded
/// for the same reason — a country larger than this is a campaign, and a campaign is out of scope.
/// </para>
/// </summary>
public sealed class OfficialNoticePublishedConsumer
    : ModuleEventConsumer<OfficialNoticePublishedIntegrationEvent>
{
    private const int AudienceLimit = 500;

    private readonly INotificationAudienceResolver _audiences;

    public OfficialNoticePublishedConsumer(
        INotificationDispatcher dispatcher, INotificationAudienceResolver audiences)
        : base(dispatcher) => _audiences = audiences;

    protected override string SourceModule => "Intelligence";

    protected override IEnumerable<NotificationTrigger> Translate(
        OfficialNoticePublishedIntegrationEvent integrationEvent) => Translate(integrationEvent, []);

    protected override async Task HandleAsync(
        OfficialNoticePublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var audience = await _audiences.ResolveCountryAudienceAsync(
            integrationEvent.CountryCode, "alerts", AudienceLimit, cancellationToken);

        foreach (var trigger in Translate(integrationEvent, audience))
        {
            await DispatchOneAsync(trigger, cancellationToken);
        }
    }

    private IEnumerable<NotificationTrigger> Translate(
        OfficialNoticePublishedIntegrationEvent integrationEvent,
        IReadOnlyList<SharedKernel.Identity.UserId> audience)
    {
        var values = Values();
        values["officialNoticeId"] = integrationEvent.OfficialNoticeId;
        values["title"] = integrationEvent.Title;
        values["summary"] = integrationEvent.Summary;
        values["countryCode"] = integrationEvent.CountryCode;

        foreach (var member in audience)
        {
            yield return Trigger(
                member,
                "country-audience",
                integrationEvent.PublishedAt,
                values,
                subjectType: "official-notice",
                subjectId: integrationEvent.OfficialNoticeId.ToString(),
                groupKey: $"notices:{integrationEvent.CountryCode}");
        }
    }
}
