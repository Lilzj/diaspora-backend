using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised on every move through the viewing workflow.
/// <para>
/// One event rather than five, for the reason the listing's status event gives: the relay decides
/// which published contract a scheduling or a completion becomes, and the domain never learns what
/// they are called.
/// </para>
/// </summary>
public sealed record HousingViewingStatusChangedDomainEvent : DomainEvent
{
    public required HousingViewingId ViewingId { get; init; }

    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required UserId SeekerUserId { get; init; }

    public required HousingViewingStatus PreviousStatus { get; init; }

    public required HousingViewingStatus CurrentStatus { get; init; }

    /// <summary>The agreed time, once there is one.</summary>
    public DateTimeOffset? ScheduledFor { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
