using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Providers.V1.Events;

/// <summary>
/// A provider's lifecycle status moved.
/// <para>
/// Carries both ends of the transition, because the direction is what a consumer branches on: a
/// move <em>into</em> <c>Active</c> puts a provider back into search, a move out of it takes it
/// out, and a move between two inactive statuses changes nothing downstream. An event carrying
/// only the new status would make every consumer keep its own copy of the old one.
/// </para>
/// </summary>
public sealed record ProviderStatusChangedIntegrationEvent : IntegrationEvent
{
    public required Guid ProviderId { get; init; }

    /// <summary>The <c>ProviderStatus</c> name before the change.</summary>
    public required string PreviousStatus { get; init; }

    /// <summary>The <c>ProviderStatus</c> name after it.</summary>
    public required string CurrentStatus { get; init; }

    /// <summary>Null when a sweeper or another module's event moved it rather than a person.</summary>
    public Guid? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// The provider's notifiable member — B13.1. Its current primary owner.
    /// <para>
    /// <b>The brief's "provider suspended", and the reason this field is here.</b> A move out of
    /// <c>Active</c> takes a business off every discovery surface it depends on, and
    /// <see cref="ChangedBy"/> is null exactly when nobody at the provider did it — a sweeper, a
    /// moderator, another module's event. That is the case where the owner has to be told, and
    /// the payload could not name them.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the provider could not be read.</para>
    /// </summary>
    public Guid? OwnerUserId { get; init; }
}
