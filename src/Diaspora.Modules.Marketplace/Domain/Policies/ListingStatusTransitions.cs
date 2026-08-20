using Diaspora.Modules.Marketplace.Domain.Enums;

namespace Diaspora.Modules.Marketplace.Domain.Policies;

/// <summary>
/// The legal moves through a listing's lifecycle, declared once.
/// <para>
/// <b>A table rather than a chain of <c>if</c> statements inside the aggregate.</b> The listing
/// has seven states and a dozen mutators, and expressing the machine inline means the rule
/// "a sold listing does not go back on sale" is written in six places and true in five. Declared
/// here it is written once, testable without constructing an aggregate, and printable into the
/// lifecycle guide.
/// </para>
/// <para>
/// <b><see cref="ListingStatus.Reserved"/> is reached by holding stock, not by a command.</b> It
/// is what the listing becomes when the last available unit goes into a reservation, and it
/// leaves the moment one comes back. That is why Active↔Reserved runs both ways here and why no
/// endpoint sets it: an operator who could set "reserved" by hand could take stock off the market
/// without a buyer attached to it.
/// </para>
/// <para>
/// <b>Sold is terminal but for archival, and deliberately so.</b> Re-listing a sold item is a new
/// listing: reopening this one would silently rewrite the history of everyone who offered on it,
/// and would leave the completed reservations pointing at a listing that says it is available.
/// </para>
/// </summary>
public static class ListingStatusTransitions
{
    private static readonly Dictionary<ListingStatus, ListingStatus[]> Allowed = new()
    {
        // A draft goes live, or is abandoned.
        [ListingStatus.Draft] =
        [
            ListingStatus.Published,
            ListingStatus.Active,
            ListingStatus.Archived
        ],

        // Published but not yet transacting — the scheduled-drop case.
        [ListingStatus.Published] =
        [
            ListingStatus.Active,
            ListingStatus.Draft,
            ListingStatus.Expired,
            ListingStatus.Archived
        ],

        // Live. Where a listing spends its life.
        [ListingStatus.Active] =
        [
            ListingStatus.Reserved,
            ListingStatus.Sold,
            ListingStatus.Expired,
            ListingStatus.Archived,
            ListingStatus.Draft
        ],

        // Every unit held. A cancelled or expired reservation brings it straight back.
        [ListingStatus.Reserved] =
        [
            ListingStatus.Active,
            ListingStatus.Sold,
            ListingStatus.Expired,
            ListingStatus.Archived
        ],

        // Terminal but for archival. See the class summary.
        [ListingStatus.Sold] =
        [
            ListingStatus.Archived
        ],

        // Expiry is reversible: extending the date brings it back.
        [ListingStatus.Expired] =
        [
            ListingStatus.Active,
            ListingStatus.Draft,
            ListingStatus.Archived
        ],

        // Archived returns to whatever it can sensibly resume as. Never straight to Active:
        // a listing that has been off the market wants its price and stock looked at first.
        [ListingStatus.Archived] =
        [
            ListingStatus.Draft
        ]
    };

    /// <summary>Whether a listing may move from one status to another.</summary>
    public static bool CanTransition(ListingStatus from, ListingStatus to) =>
        from != to
        && Allowed.TryGetValue(from, out var targets)
        && Array.IndexOf(targets, to) >= 0;

    /// <summary>
    /// Everything a listing in this status may become. For the documentation and the client's
    /// action bar, which should not have to hard-code the machine a second time.
    /// </summary>
    public static IReadOnlyList<ListingStatus> From(ListingStatus status) =>
        Allowed.TryGetValue(status, out var targets) ? targets : [];

    /// <summary>Whether a listing in this status may appear on a public surface.</summary>
    public static bool IsPubliclyVisible(ListingStatus status) =>
        status is not (ListingStatus.Draft or ListingStatus.Archived);

    /// <summary>
    /// Whether a listing in this status accepts offers and reservations.
    /// <para>
    /// <see cref="ListingStatus.Reserved"/> is deliberately excluded: a listing whose stock is
    /// entirely spoken for must not take a second reservation, and an offer on it would be an
    /// offer on something the seller cannot sell.
    /// </para>
    /// </summary>
    public static bool AcceptsCommitments(ListingStatus status) =>
        status is ListingStatus.Active;

    /// <summary>Whether trading on this listing has ended, one way or another.</summary>
    public static bool IsTerminal(ListingStatus status) =>
        status is ListingStatus.Sold or ListingStatus.Archived;
}
