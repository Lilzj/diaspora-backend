using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Providers.V1.Events;

/// <summary>
/// A provider's operating hours, availability mode or dated exceptions changed.
/// <para>
/// One event for all three, because every consumer treats them the same way: an open-now
/// projection has to be recomputed, and it does not matter whether Tuesday's hours moved or a
/// holiday was declared.
/// </para>
/// <para>
/// <see cref="AffectsToday"/> is what keeps this cheap. A provider setting next Christmas's
/// hours in July does not need any open-now cache invalidated today, and a consumer that cannot
/// tell the difference invalidates on every schedule edit the platform ever sees.
/// </para>
/// </summary>
public sealed record ProviderAvailabilityChangedIntegrationEvent : IntegrationEvent
{
    public required Guid ProviderId { get; init; }

    /// <summary>The <c>AvailabilityKind</c> name.</summary>
    public required string Kind { get; init; }

    /// <summary>Whether the change alters whether the provider is open right now.</summary>
    public required bool AffectsToday { get; init; }

    /// <summary>The earliest date the change takes effect. Null when already in effect.</summary>
    public DateOnly? EffectiveFrom { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
