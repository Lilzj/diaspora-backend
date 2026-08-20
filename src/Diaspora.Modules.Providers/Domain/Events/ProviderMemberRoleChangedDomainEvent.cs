using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when somebody's authority over a provider moves in either direction.
/// <para>
/// Carries both roles because a promotion and a demotion need different notifications, and
/// because an authorization cache keyed on this provider must be invalidated on a demotion
/// urgently and on a promotion merely eventually.
/// </para>
/// </summary>
public sealed record ProviderMemberRoleChangedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required UserId UserId { get; init; }

    public required ProviderRole PreviousRole { get; init; }

    public required ProviderRole CurrentRole { get; init; }

    public required UserId ChangedBy { get; init; }
}
