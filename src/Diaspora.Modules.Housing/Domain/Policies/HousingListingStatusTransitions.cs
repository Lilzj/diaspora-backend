using Diaspora.Modules.Housing.Domain.Enums;

namespace Diaspora.Modules.Housing.Domain.Policies;

/// <summary>
/// The legal moves through a listing's lifecycle, declared once.
/// <para>
/// <b>A table rather than a chain of <c>if</c> statements inside the aggregate.</b> The listing
/// has seven states and a dozen mutators, and expressing the machine inline means the rule "an
/// occupied property does not go back on the market without somebody looking at it" is written in
/// six places and true in five. Declared here it is written once, testable without constructing an
/// aggregate, and printable into the lifecycle guide. B10 records the same reasoning.
/// </para>
/// <para>
/// <b><see cref="HousingListingStatus.Reserved"/> is reached by holding units, not by a
/// command.</b> It is what a listing becomes when the last available unit goes into a reservation,
/// and it leaves the moment one comes back. That is why Available↔Reserved runs both ways here and
/// why no endpoint sets it: an operator who could set "reserved" by hand could take a property off
/// the market with nobody attached to it.
/// </para>
/// <para>
/// <b>Occupied is not terminal, and this is where Housing genuinely differs from Marketplace.</b>
/// A sold bicycle is gone; a let flat comes back, because tenancies end. Occupied therefore
/// returns to Available and to Unavailable — the two things that happen when a tenant leaves and
/// the landlord either re-lets or redecorates. Re-listing is the normal case here rather than the
/// suspicious one.
/// </para>
/// <para>
/// <b>Archived returns only to Draft.</b> A property that has been off the market wants its rent,
/// its availability date and its photographs looked at before anybody is shown it again, and
/// routing the restore through Draft is what forces that.
/// </para>
/// </summary>
public static class HousingListingStatusTransitions
{
    private static readonly Dictionary<HousingListingStatus, HousingListingStatus[]> Allowed =
        new()
        {
            // A draft goes live, or is abandoned. Straight to Available when the property is
            // ready now, which is the common case for a room somebody is letting this week.
            [HousingListingStatus.Draft] =
            [
                HousingListingStatus.Published,
                HousingListingStatus.Available,
                HousingListingStatus.Archived
            ],

            // Discoverable, not yet ready to move into. The flat advertised in March for June.
            [HousingListingStatus.Published] =
            [
                HousingListingStatus.Available,
                HousingListingStatus.Unavailable,
                HousingListingStatus.Draft,
                HousingListingStatus.Archived
            ],

            // Live and ready. Where a listing spends most of its life.
            [HousingListingStatus.Available] =
            [
                HousingListingStatus.Reserved,
                HousingListingStatus.Occupied,
                HousingListingStatus.Unavailable,
                HousingListingStatus.Draft,
                HousingListingStatus.Archived
            ],

            // Every unit held. A cancelled, declined or expired reservation brings it back.
            [HousingListingStatus.Reserved] =
            [
                HousingListingStatus.Available,
                HousingListingStatus.Occupied,
                HousingListingStatus.Unavailable,
                HousingListingStatus.Archived
            ],

            // Somebody lives there. Tenancies end, so this is not terminal — see the summary.
            [HousingListingStatus.Occupied] =
            [
                HousingListingStatus.Available,
                HousingListingStatus.Unavailable,
                HousingListingStatus.Archived
            ],

            // Off the market for now. Returns without republication, which is the whole point
            // of the state: a landlord redecorating for a fortnight should not have to publish
            // again and lose the listing's age and its view count.
            [HousingListingStatus.Unavailable] =
            [
                HousingListingStatus.Available,
                HousingListingStatus.Published,
                HousingListingStatus.Draft,
                HousingListingStatus.Archived
            ],

            // Archived returns to Draft and nowhere else. See the class summary.
            [HousingListingStatus.Archived] =
            [
                HousingListingStatus.Draft
            ]
        };

    /// <summary>Whether a listing may move from one status to another.</summary>
    public static bool CanTransition(HousingListingStatus from, HousingListingStatus to) =>
        from != to
        && Allowed.TryGetValue(from, out var targets)
        && Array.IndexOf(targets, to) >= 0;

    /// <summary>
    /// Everything a listing in this status may become. For the documentation and the client's
    /// action bar, which should not have to hard-code the machine a second time.
    /// </summary>
    public static IReadOnlyList<HousingListingStatus> From(HousingListingStatus status) =>
        Allowed.TryGetValue(status, out var targets) ? targets : [];

    /// <summary>Whether a listing in this status may appear on a public surface.</summary>
    public static bool IsPubliclyVisible(HousingListingStatus status) =>
        status is not (HousingListingStatus.Draft or HousingListingStatus.Archived);

    /// <summary>
    /// Whether a listing in this status accepts viewing requests.
    /// <para>
    /// <b>Wider than the reservation gate, and that is the point.</b> Somebody should be able to
    /// look round a property that is published for a future date, and round one that is currently
    /// reserved — because reservations fall through, and the second person in the queue is who the
    /// landlord calls when they do. Only an occupied, withdrawn or archived property stops taking
    /// visits.
    /// </para>
    /// </summary>
    public static bool AcceptsViewings(HousingListingStatus status) =>
        status is HousingListingStatus.Published
            or HousingListingStatus.Available
            or HousingListingStatus.Reserved;

    /// <summary>
    /// Whether a listing in this status accepts reservations.
    /// <para>
    /// <see cref="HousingListingStatus.Reserved"/> is excluded here and included above: a property
    /// whose units are entirely spoken for must not take a second hold. Whether units remain is
    /// the availability value object's question, asked separately.
    /// </para>
    /// </summary>
    public static bool AcceptsReservations(HousingListingStatus status) =>
        status is HousingListingStatus.Published or HousingListingStatus.Available;

    /// <summary>Whether this listing's public life has ended.</summary>
    public static bool IsTerminal(HousingListingStatus status) =>
        status is HousingListingStatus.Archived;
}
