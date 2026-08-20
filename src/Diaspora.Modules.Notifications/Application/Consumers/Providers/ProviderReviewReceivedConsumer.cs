using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Providers;

/// <summary>
/// A member reviewed a provider.
/// <para>
/// <b>Both parties, since B13.1.</b> The reviewer is confirmed that their review was published; the
/// provider's owner is told they received one — the client's original "New review from Amara K.",
/// which was the first item in its own mock notification list and could not be sent until B7 put
/// <c>OwnerUserId</c> on the contract. Before that, the payload named the reviewer and nobody at the
/// business the review was about.
/// </para>
/// <para>
/// The owner's message carries the rating and whether anything was written, and not the text. A
/// notification is a prompt to go and read something, and a one-star review quoted in a push
/// notification is the platform delivering an insult rather than a fact.
/// </para>
/// </summary>
public sealed class ProviderReviewReceivedConsumer
    : ModuleEventConsumer<ProviderReviewReceivedIntegrationEvent>
{
    public ProviderReviewReceivedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Providers";

    protected override IEnumerable<NotificationTrigger> Translate(
        ProviderReviewReceivedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["providerId"] = integrationEvent.ProviderId;
        values["rating"] = integrationEvent.Rating;
        values["hasBody"] = integrationEvent.HasBody;

        yield return Trigger(
            UserId.From(integrationEvent.ReviewerId),
            "reviewer",
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
