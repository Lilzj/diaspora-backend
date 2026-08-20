using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>Raised when somebody gains a role on a provider.</summary>
public sealed record ProviderMemberAddedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required UserId UserId { get; init; }

    public required ProviderRole Role { get; init; }

    /// <summary>Null for the founding owner, who added themselves at registration.</summary>
    public UserId? InvitedBy { get; init; }
}
