using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when a provider's operating hours, availability mode or dated exceptions changed.
/// <para>
/// One event for all three because every consumer treats them the same way: an open-now
/// projection has to be recomputed, and it does not matter whether Tuesday's hours moved or a
/// holiday was declared. Three events would have made every consumer subscribe to all three.
/// </para>
/// <para>
/// <see cref="AffectsToday"/> is the field that keeps this cheap. A provider setting next
/// Christmas's hours in July does not need any open-now cache invalidated today, and a consumer
/// that cannot tell the difference invalidates on every schedule edit the platform ever sees.
/// </para>
/// </summary>
public sealed record ProviderAvailabilityChangedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required AvailabilityKind Kind { get; init; }

    /// <summary>Whether the change alters whether the provider is open right now.</summary>
    public required bool AffectsToday { get; init; }

    /// <summary>The earliest date the change takes effect. Null when it is already in effect.</summary>
    public DateOnly? EffectiveFrom { get; init; }

    public UserId? ChangedBy { get; init; }
}
