using Diaspora.Modules.Marketplace.Domain.Enums;

namespace Diaspora.Modules.Marketplace.Domain.Policies;

/// <summary>
/// The legal moves through a reservation's life.
/// <para>
/// Small enough to read at a glance, and declared here anyway for the same reason the listing's
/// is: the rule that a completed reservation never reopens has to be true in one place rather
/// than at four call sites.
/// </para>
/// <para>
/// <b>Every terminal state is reached exactly once, and each one settles the stock.</b>
/// <see cref="ReservationStatus.Completed"/> converts held units to sold;
/// <see cref="ReservationStatus.Cancelled"/> and <see cref="ReservationStatus.Expired"/> return
/// them. There is no fourth exit, because a fourth exit would be units that are neither on the
/// market nor accounted for.
/// </para>
/// </summary>
public static class ReservationStatusTransitions
{
    private static readonly Dictionary<ReservationStatus, ReservationStatus[]> Allowed = new()
    {
        [ReservationStatus.Pending] =
        [
            ReservationStatus.AwaitingCollection,
            ReservationStatus.Cancelled,
            ReservationStatus.Expired
        ],

        [ReservationStatus.AwaitingCollection] =
        [
            ReservationStatus.Completed,
            ReservationStatus.Cancelled,
            ReservationStatus.Expired
        ],

        [ReservationStatus.Completed] = [],

        [ReservationStatus.Cancelled] = [],

        [ReservationStatus.Expired] = []
    };

    public static bool CanTransition(ReservationStatus from, ReservationStatus to) =>
        from != to
        && Allowed.TryGetValue(from, out var targets)
        && Array.IndexOf(targets, to) >= 0;

    public static IReadOnlyList<ReservationStatus> From(ReservationStatus status) =>
        Allowed.TryGetValue(status, out var targets) ? targets : [];

    /// <summary>Whether the reservation is still holding stock.</summary>
    public static bool IsHoldingStock(ReservationStatus status) =>
        status is ReservationStatus.Pending or ReservationStatus.AwaitingCollection;

    /// <summary>Whether the reservation has finished, however it finished.</summary>
    public static bool IsClosed(ReservationStatus status) => !IsHoldingStock(status);
}
