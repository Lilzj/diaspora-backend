using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>
/// An endpoint stopped being usable.
/// <para>
/// Never published. Which devices a member holds is a statement about their life — one phone or
/// four, an iPad, a work laptop — and no module outside Communication has any business knowing when
/// one goes away.
/// </para>
/// </summary>
public sealed record DeviceRetiredDomainEvent : DomainEvent
{
    public required DeviceRegistrationId DeviceRegistrationId { get; init; }

    public required UserId UserId { get; init; }

    /// <summary><c>Removed</c> by the member, or <c>Rejected</c> by the provider.</summary>
    public required string Reason { get; init; }

    public required DateTimeOffset RetiredAt { get; init; }
}
