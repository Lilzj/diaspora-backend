using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when a provider starts offering something new.
/// <para>
/// One of the eight events the brief names for notification readiness, and the one with the
/// clearest member-facing value: somebody following a caterer wants to know a new dish is on
/// the menu. B7 publishes it and stops; B16 decides who hears about it.
/// </para>
/// </summary>
public sealed record ProviderServiceAddedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required ProviderServiceId ServiceId { get; init; }

    public required string Name { get; init; }

    public ServiceCategoryId? CategoryId { get; init; }

    public UserId? AddedBy { get; init; }
}
