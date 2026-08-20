using Diaspora.Modules.Housing.Domain.Enums;

namespace Diaspora.Modules.Housing.Domain.Policies;

/// <summary>
/// The legal moves through a property visit.
/// <para>
/// <b>Nothing here holds a unit</b>, and that is the difference between this machine and the
/// reservation's. A viewing is an appointment: ten people may hold one on the same flat on the
/// same Saturday, and the property stays on the market throughout. It is precisely because
/// viewings are cheap that reservations can afford to be expensive.
/// </para>
/// <para>
/// <b><see cref="HousingViewingStatus.NoShow"/> is recorded rather than folded into cancellation,
/// and it is recorded against whoever failed to appear.</b> A pattern of it is the most useful
/// signal a landlord has about a time-waster and the most useful signal a member has about a
/// landlord who advertises a property that does not exist — which is what an advance-fee rental
/// fraud looks like from the outside.
/// </para>
/// </summary>
public static class HousingViewingStatusTransitions
{
    private static readonly Dictionary<HousingViewingStatus, HousingViewingStatus[]> Allowed =
        new()
        {
            // Asked for, not yet answered.
            [HousingViewingStatus.Requested] =
            [
                HousingViewingStatus.Scheduled,
                HousingViewingStatus.Cancelled,
                HousingViewingStatus.Expired
            ],

            // A time is agreed. Both no-show and completion follow from here.
            [HousingViewingStatus.Scheduled] =
            [
                HousingViewingStatus.Completed,
                HousingViewingStatus.NoShow,
                HousingViewingStatus.Cancelled,
                HousingViewingStatus.Expired
            ],

            [HousingViewingStatus.Completed] = [],
            [HousingViewingStatus.Cancelled] = [],
            [HousingViewingStatus.NoShow] = [],
            [HousingViewingStatus.Expired] = []
        };

    /// <summary>Whether a viewing may move from one status to another.</summary>
    public static bool CanTransition(HousingViewingStatus from, HousingViewingStatus to) =>
        from != to
        && Allowed.TryGetValue(from, out var targets)
        && Array.IndexOf(targets, to) >= 0;

    /// <summary>Everything a viewing in this status may become.</summary>
    public static IReadOnlyList<HousingViewingStatus> From(HousingViewingStatus status) =>
        Allowed.TryGetValue(status, out var targets) ? targets : [];

    /// <summary>Whether the visit is still ahead of the parties.</summary>
    public static bool IsOpen(HousingViewingStatus status) =>
        status is HousingViewingStatus.Requested or HousingViewingStatus.Scheduled;

    /// <summary>Whether the visit has finished, one way or another.</summary>
    public static bool IsClosed(HousingViewingStatus status) => !IsOpen(status);

    /// <summary>
    /// Whether this viewing satisfies a landlord's viewing-before-reservation requirement.
    /// <para>
    /// Only a completed one does. A scheduled viewing is a promise, and a landlord who asked for
    /// a visit first meant a visit rather than an intention to visit.
    /// </para>
    /// </summary>
    public static bool SatisfiesViewingRequirement(HousingViewingStatus status) =>
        status is HousingViewingStatus.Completed;
}
