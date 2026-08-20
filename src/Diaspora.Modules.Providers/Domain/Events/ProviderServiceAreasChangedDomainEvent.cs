using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when where a provider will travel to changed.
/// <para>
/// Its own event rather than a generic update, because service areas are the one part of a
/// provider that changes <em>which members can find it at all</em>. A search index that treats a
/// service-area edit as an ordinary field change leaves a mobile hairdresser invisible in the
/// three boroughs she just added.
/// </para>
/// </summary>
public sealed record ProviderServiceAreasChangedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required int AreaCount { get; init; }

    /// <summary>Whether the provider now claims to serve remotely, which no radius can express.</summary>
    public required bool ServesRemotely { get; init; }

    public UserId? ChangedBy { get; init; }
}
