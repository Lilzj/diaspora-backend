using Diaspora.Modules.Housing.Domain.Enums;

namespace Diaspora.Modules.Housing.Domain.Policies;

/// <summary>
/// The legal moves through a reservation, and the single place that says which states still hold
/// units.
/// <para>
/// <b><see cref="HoldsUnits"/> is the most consequential method in the module.</b> Every state on
/// the left of the terminal wall is a room nobody else can take, and a state accidentally left out
/// of it is a property that stays off the market forever. It is expressed here rather than as a
/// property on the aggregate so the reservation, the listing and the expiry sweep all read the
/// same answer.
/// </para>
/// <para>
/// <b>The path is deliberately not a straight line.</b> A reservation may be requested and go
/// straight to occupied — a hostel bed taken for tonight never passes through a signed lease — and
/// it may be cancelled from any live state, by either party, at any time. A workflow that could
/// trap somebody in a reservation they wanted out of would be the platform working against the
/// person it was meant to serve.
/// </para>
/// </summary>
public static class HousingReservationStatusTransitions
{
    private static readonly Dictionary<HousingReservationStatus, HousingReservationStatus[]>
        Allowed = new()
        {
            // The member has asked. Units are already held: the aggregate explains why.
            [HousingReservationStatus.Requested] =
            [
                HousingReservationStatus.Reserved,
                HousingReservationStatus.LeaseSigned,
                HousingReservationStatus.Occupied,
                HousingReservationStatus.Declined,
                HousingReservationStatus.Cancelled,
                HousingReservationStatus.Expired
            ],

            // The landlord has agreed to hold it.
            [HousingReservationStatus.Reserved] =
            [
                HousingReservationStatus.LeaseSigned,
                HousingReservationStatus.Occupied,
                HousingReservationStatus.Cancelled,
                HousingReservationStatus.Expired
            ],

            // The parties report an agreement. Housing stores the fact, never the document.
            // Still cancellable: agreements fall through between signing and moving in more
            // often than any other point in this workflow.
            [HousingReservationStatus.LeaseSigned] =
            [
                HousingReservationStatus.Occupied,
                HousingReservationStatus.Cancelled
            ],

            // Moved in. Terminal, and the only terminal state that does not return units to the
            // market — they are occupied rather than free.
            [HousingReservationStatus.Occupied] = [],

            [HousingReservationStatus.Cancelled] = [],
            [HousingReservationStatus.Declined] = [],
            [HousingReservationStatus.Expired] = []
        };

    /// <summary>Whether a reservation may move from one status to another.</summary>
    public static bool CanTransition(
        HousingReservationStatus from, HousingReservationStatus to) =>
        from != to
        && Allowed.TryGetValue(from, out var targets)
        && Array.IndexOf(targets, to) >= 0;

    /// <summary>Everything a reservation in this status may become.</summary>
    public static IReadOnlyList<HousingReservationStatus> From(HousingReservationStatus status) =>
        Allowed.TryGetValue(status, out var targets) ? targets : [];

    /// <summary>
    /// Whether this reservation is still keeping units off the market.
    /// <para>
    /// See the class summary: this is the method a missing state turns into a permanently
    /// unlettable property.
    /// </para>
    /// </summary>
    public static bool HoldsUnits(HousingReservationStatus status) =>
        status is HousingReservationStatus.Requested
            or HousingReservationStatus.Reserved
            or HousingReservationStatus.LeaseSigned;

    /// <summary>Whether the reservation has finished, one way or another.</summary>
    public static bool IsClosed(HousingReservationStatus status) =>
        status is HousingReservationStatus.Occupied
            or HousingReservationStatus.Cancelled
            or HousingReservationStatus.Declined
            or HousingReservationStatus.Expired;

    /// <summary>
    /// Whether the reservation ended without anybody moving in. What the abandonment counter
    /// measures, and what returns units rather than converting them.
    /// </summary>
    public static bool IsAbandoned(HousingReservationStatus status) =>
        status is HousingReservationStatus.Cancelled
            or HousingReservationStatus.Declined
            or HousingReservationStatus.Expired;
}
