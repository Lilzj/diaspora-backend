using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Providers.V1.Events;

/// <summary>
/// A member reviewed a provider.
/// <para>
/// The client's existing "New review from Amara K." notification — the first item in its own mock
/// notification list — becomes real when B16 subscribes to this. B7 publishes it and implements
/// no delivery.
/// </para>
/// </summary>
public sealed record ProviderReviewReceivedIntegrationEvent : IntegrationEvent
{
    public required Guid ReviewId { get; init; }

    public required Guid ProviderId { get; init; }

    public required Guid ReviewerId { get; init; }

    /// <summary>One to five.</summary>
    public required int Rating { get; init; }

    /// <summary>Whether the member wrote anything beyond the stars.</summary>
    public required bool HasBody { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>
    /// The provider's notifiable member — B13.1. Its current primary owner.
    /// <para>
    /// <b>The enrichment that makes the notification in the summary above real.</b> "New review
    /// from Amara K." is addressed to whoever runs the business, and the payload named only the
    /// reviewer — so the only honest message this contract could produce was a receipt to the
    /// person who wrote the review.
    /// </para>
    /// <para>
    /// One named owner, not the staff list. A provider is a team and a fan-out across it is a
    /// decision for the module that models teams.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the provider could not be read.</para>
    /// </summary>
    public Guid? OwnerUserId { get; init; }
}
