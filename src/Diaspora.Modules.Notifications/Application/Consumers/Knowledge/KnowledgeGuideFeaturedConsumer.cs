using Diaspora.Contracts.Knowledge.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Modules.Notifications.Extensibility.Audiences;

namespace Diaspora.Modules.Notifications.Application.Consumers.Knowledge;

/// <summary>
/// A guide was featured on one country's knowledge directory.
/// <para>
/// <b>The brief's "New immigration guide published", and the one consumer that needs an audience
/// rather than a person.</b> The event names a country, not a member — which is correct, because the
/// Knowledge Directory has no business holding a list of who lives where. This module asks
/// <see cref="INotificationAudienceResolver"/>, whose shipped implementation answers "nobody" until
/// an adapter or a subscription model exists.
/// </para>
/// <para>
/// <b>No recommendation logic anywhere.</b> The directory decided this guide is worth featuring, at
/// this position, with this note. This module repeats that decision and never forms one — which is
/// what the brief means by "consume published knowledge".
/// </para>
/// </summary>
public sealed class KnowledgeGuideFeaturedConsumer
    : ModuleEventConsumer<KnowledgeGuideFeaturedIntegrationEvent>
{
    /// <summary>
    /// Members told about one featuring.
    /// <para>
    /// Bounded so a broadcast cannot hold a transaction open across a hundred thousand inserts. A
    /// country audience larger than this is a campaign, which is explicitly out of scope.
    /// </para>
    /// </summary>
    private const int AudienceLimit = 500;

    private readonly INotificationAudienceResolver _audiences;

    public KnowledgeGuideFeaturedConsumer(
        INotificationDispatcher dispatcher, INotificationAudienceResolver audiences)
        : base(dispatcher) => _audiences = audiences;

    protected override string SourceModule => "Knowledge";

    protected override IEnumerable<NotificationTrigger> Translate(
        KnowledgeGuideFeaturedIntegrationEvent integrationEvent) => Translate(integrationEvent, []);

    protected override async Task HandleAsync(
        KnowledgeGuideFeaturedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var audience = await _audiences.ResolveCountryAudienceAsync(
            integrationEvent.CountryCode, "guides", AudienceLimit, cancellationToken);

        if (audience.Count == 0)
        {
            return;
        }

        foreach (var trigger in Translate(integrationEvent, audience))
        {
            await DispatchOneAsync(trigger, cancellationToken);
        }
    }

    private IEnumerable<NotificationTrigger> Translate(
        KnowledgeGuideFeaturedIntegrationEvent integrationEvent,
        IReadOnlyList<SharedKernel.Identity.UserId> audience)
    {
        var values = Values();
        values["guideId"] = integrationEvent.GuideId;
        values["countryCode"] = integrationEvent.CountryCode;
        values["knowledgeCategoryKey"] = integrationEvent.KnowledgeCategoryKey ?? string.Empty;
        values["note"] = integrationEvent.Note ?? string.Empty;

        foreach (var member in audience)
        {
            yield return Trigger(
                member,
                "country-audience",
                integrationEvent.ChangedAt,
                values,
                subjectType: "guide",
                subjectId: integrationEvent.GuideId.ToString(),
                groupKey: $"knowledge:{integrationEvent.CountryCode}:guides");
        }
    }
}
