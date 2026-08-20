using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised whenever a provider's lifecycle status moves.
/// <para>
/// Carries both ends of the transition, because the direction is what a consumer branches on: a
/// move <em>into</em> <see cref="ProviderStatus.Active"/> puts a provider back into search, a
/// move out of it takes it out, and a move between two inactive statuses changes nothing
/// downstream. An event carrying only the new status would make every consumer keep its own
/// copy of the old one.
/// </para>
/// </summary>
public sealed record ProviderStatusChangedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required ProviderStatus PreviousStatus { get; init; }

    public required ProviderStatus CurrentStatus { get; init; }

    /// <summary>Null when a sweeper or an integration event moved it rather than a person.</summary>
    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    /// <summary>
    /// The provider's longest-standing owner, stamped by the aggregate — B13.1. The recipient
    /// for a suspension, which is exactly the transition <see cref="ChangedBy"/> is null for.
    /// </summary>
    public UserId? OwnerUserId { get; init; }
}
