using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>Raised when a provider stops offering something.</summary>
public sealed record ProviderServiceRemovedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required ProviderServiceId ServiceId { get; init; }

    public required string Name { get; init; }

    public UserId? RemovedBy { get; init; }
}
