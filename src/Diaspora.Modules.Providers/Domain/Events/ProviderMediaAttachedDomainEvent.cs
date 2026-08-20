using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when a provider attaches an asset to itself.
/// <para>
/// Its whole purpose is to trigger the claim to Media. B5 sweeps assets nothing ever attached,
/// so a logo uploaded and referenced but never <em>claimed</em> disappears within a day —
/// silently, because every upload reported success. This event is what makes the claim happen
/// outside the write transaction, so a Media outage cannot fail a profile edit.
/// </para>
/// </summary>
public sealed record ProviderMediaAttachedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required MediaId MediaId { get; init; }

    /// <summary>A <c>ProviderMediaRole</c> slug: <c>cover</c>, <c>logo</c>, <c>gallery</c>, <c>menu</c>.</summary>
    public required string Role { get; init; }

    public required int Position { get; init; }
}
