using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>Raised when somebody loses their role on a provider entirely.</summary>
public sealed record ProviderMemberRemovedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required UserId UserId { get; init; }

    public required ProviderRole PreviousRole { get; init; }

    /// <summary>Equal to <see cref="UserId"/> when the person removed themselves.</summary>
    public required UserId RemovedBy { get; init; }
}
