using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A seeker's application on a property moved — B23.x Wave 12.
/// <para>
/// <b>B11 published only the endings.</b> <c>HousingReservationStatusChangedDomainEvent</c> is raised
/// on every move through the workflow, and the relay translated exactly three of them —
/// <c>Cancelled</c>, <c>Declined</c>, <c>Expired</c> — into <c>ReservationCancelledIntegrationEvent</c>.
/// That was the right shape for the consumers it was written for: units returning is what a search
/// index and an availability watcher care about. It leaves the seeker unable to learn the one thing
/// they are actually waiting for — that the landlord <em>agreed</em>. "My housing application status
/// changes" is a member watching for <c>Reserved</c> and <c>LeaseSigned</c> as much as for
/// <c>Declined</c>.
/// </para>
/// <para>
/// <b>Additive rather than a change to the cancellation contract.</b> Widening
/// <c>ReservationCancelledIntegrationEvent</c> to carry forward transitions would make an event named
/// for an ending fire on a beginning, and every existing consumer — the search index, the
/// availability watcher, the analytics rollup — would start counting lettings that had not ended. A
/// second contract costs one row in the outbox and breaks nobody.
/// </para>
/// <para>
/// <b>This is the seeker's fact.</b> It names the seeker because it is about their application, and
/// the landlord because they own the property. It carries no message, no reason the landlord wrote
/// for themselves, and no address.
/// </para>
/// </summary>
public sealed record HousingApplicationStatusChangedIntegrationEvent : IntegrationEvent
{
    public required Guid ReservationId { get; init; }

    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    /// <summary>The member whose application this is. The person the change is about.</summary>
    public required Guid SeekerUserId { get; init; }

    /// <summary>
    /// Where it was, as its name — <c>Requested</c>, <c>Reserved</c>, <c>LeaseSigned</c>,
    /// <c>Occupied</c>, <c>Cancelled</c>, <c>Declined</c>, <c>Expired</c>.
    /// </summary>
    public required string PreviousStatus { get; init; }

    /// <summary>Where it is now, in the same vocabulary.</summary>
    public required string CurrentStatus { get; init; }

    /// <summary>
    /// True when the application is over, whatever the outcome.
    /// <para>
    /// Carried so a consumer can distinguish "moved forward" from "finished" without holding a copy of
    /// B11's status enum and its terminal set — which is exactly the copy that goes stale when a
    /// status is added.
    /// </para>
    /// </summary>
    public required bool IsTerminal { get; init; }

    /// <summary>True on the successful ending only, so a consumer need not compare status names.</summary>
    public required bool IsSuccessful { get; init; }

    /// <summary>Who moved it, where a person did. Null when a sweep expired the hold.</summary>
    public Guid? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
