using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Events;

/// <summary>
/// Raised when an asset is soft-deleted.
/// <para>
/// Soft, not hard. The row survives so an audit can answer "what was attached to that listing
/// before it was edited", and the bytes are removed by the sweeper on its own schedule — which
/// is also what makes deletion cheap enough to be synchronous.
/// </para>
/// </summary>
public sealed record MediaAssetDeletedDomainEvent : DomainEvent
{
    public required MediaId MediaId { get; init; }

    public required UserId DeletedBy { get; init; }
}
