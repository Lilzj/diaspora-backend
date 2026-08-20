using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Providers;

/// <summary>
/// A member recommended, endorsed or wrote a testimonial for a provider.
/// <para>
/// The recommender is acknowledged and, since B13.1, so is the provider's owner — somebody vouching
/// for a business publicly is unambiguously good news for the person who runs it. See
/// <see cref="ProviderReviewReceivedConsumer"/> for the enrichment both contracts gained.
/// </para>
/// </summary>
public sealed class ProviderRecommendationReceivedConsumer
    : ModuleEventConsumer<ProviderRecommendationReceivedIntegrationEvent>
{
    public ProviderRecommendationReceivedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher) { }

    protected override string SourceModule => "Providers";

    protected override IEnumerable<NotificationTrigger> Translate(
        ProviderRecommendationReceivedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["providerId"] = integrationEvent.ProviderId;
        values["kind"] = integrationEvent.Kind;

        yield return Trigger(
            UserId.From(integrationEvent.RecommenderId),
            "recommender",
            integrationEvent.ReceivedAt,
            values,
            subjectType: "provider",
            subjectId: integrationEvent.ProviderId.ToString());

        if (integrationEvent.OwnerUserId is not { } ownerUserId)
        {
            yield break;
        }

        yield return Trigger(
            UserId.From(ownerUserId),
            "owner",
            integrationEvent.ReceivedAt,
            values,
            subjectType: "provider",
            subjectId: integrationEvent.ProviderId.ToString());
    }
}
