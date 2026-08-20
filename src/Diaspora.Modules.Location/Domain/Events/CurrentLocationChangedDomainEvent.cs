using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>
/// Raised when a member's active discovery location moves to a different place.
/// <para>
/// Not raised when the same place is re-selected. Recommendations and feed modules
/// recompute on this, and a member re-picking their own neighbourhood from the recents list
/// should not trigger that work.
/// </para>
/// </summary>
public sealed record CurrentLocationChangedDomainEvent : DomainEvent
{
    public required LocationProfileId ProfileId { get; init; }

    public required UserId UserId { get; init; }

    public required string CountryCode { get; init; }

    public AreaId? AreaId { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Whether the move was a travel-mode destination rather than a change of home.</summary>
    public required bool TravelMode { get; init; }
}
