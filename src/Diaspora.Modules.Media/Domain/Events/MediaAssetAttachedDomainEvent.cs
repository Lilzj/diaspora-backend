using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Events;

/// <summary>
/// Raised when an asset is claimed by an owning entity.
/// <para>
/// The signal that an upload has stopped being an orphan. The sweeper deletes unclaimed
/// assets after their grace period, and this is what takes an asset out of that population —
/// so a vertical that forgets to attach what it uploaded finds out within a day rather than
/// discovering a silent leak a year later.
/// </para>
/// </summary>
public sealed record MediaAssetAttachedDomainEvent : DomainEvent
{
    public required MediaId MediaId { get; init; }

    public required MediaOwnerType OwnerType { get; init; }

    public required Guid OwnerId { get; init; }

    public required string Role { get; init; }
}
