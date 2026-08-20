using Diaspora.Contracts.Knowledge.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Modules.Notifications.Extensibility.Audiences;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Knowledge;

/// <summary>
/// A country's learning path changed.
/// <para>
/// The brief's "learning path updated". Like the featuring event, it names a country rather than a
/// member, so it goes through the audience seam — and reaches nobody until one is registered.
/// </para>
/// <para>
/// Only a published path produces anything. A draft being edited is editorial work in progress, and
/// telling members about it would be telling them about a document they cannot read.
/// </para>
/// </summary>
public sealed class LearningPathUpdatedConsumer
    : ModuleEventConsumer<LearningPathUpdatedIntegrationEvent>
{
    private const int AudienceLimit = 500;
    private const string PublishedStatus = "Published";

    private readonly INotificationAudienceResolver _audiences;

    public LearningPathUpdatedConsumer(
        INotificationDispatcher dispatcher, INotificationAudienceResolver audiences)
        : base(dispatcher) => _audiences = audiences;

    protected override string SourceModule => "Knowledge";

    protected override IEnumerable<NotificationTrigger> Translate(
        LearningPathUpdatedIntegrationEvent integrationEvent) => Translate(integrationEvent, []);

    protected override async Task HandleAsync(
        LearningPathUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!string.Equals(integrationEvent.Status, PublishedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var audience = await _audiences.ResolveCountryAudienceAsync(
            integrationEvent.CountryCode, "learning", AudienceLimit, cancellationToken);

        foreach (var trigger in Translate(integrationEvent, audience))
        {
            await DispatchOneAsync(trigger, cancellationToken);
        }
    }

    private IEnumerable<NotificationTrigger> Translate(
        LearningPathUpdatedIntegrationEvent integrationEvent, IReadOnlyList<UserId> audience)
    {
        var values = Values();
        values["title"] = integrationEvent.Title;
        values["countryCode"] = integrationEvent.CountryCode;
        values["change"] = integrationEvent.Change;
        values["stepCount"] = integrationEvent.StepCount;
        values["learningPathKey"] = integrationEvent.Key;

        foreach (var member in audience)
        {
            yield return Trigger(
                member,
                "country-audience",
                integrationEvent.ChangedAt,
                values,
                subjectType: "learning-path",
                subjectId: integrationEvent.LearningPathId.ToString(),
                count: integrationEvent.StepCount,
                groupKey: $"knowledge:{integrationEvent.CountryCode}:learning");
        }
    }
}
