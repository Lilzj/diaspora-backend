using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when who may see a provider changed.
/// <para>
/// Distinct from a status change because it is the one transition that can <em>narrow</em> an
/// audience without stopping the provider operating — and a search index that misses it keeps
/// serving a provider to members it has just been hidden from, which is a privacy failure rather
/// than a stale cache.
/// </para>
/// </summary>
public sealed record ProviderVisibilityChangedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required ProviderVisibilityLevel PreviousLevel { get; init; }

    public required ProviderVisibilityLevel CurrentLevel { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public UserId? ChangedBy { get; init; }
}
