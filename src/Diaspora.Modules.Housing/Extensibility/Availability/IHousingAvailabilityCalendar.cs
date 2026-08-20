using Diaspora.Modules.Housing.Domain.Identifiers;

namespace Diaspora.Modules.Housing.Extensibility.Availability;

/// <summary>
/// The future-ready abstraction for a real availability calendar.
/// <para>
/// <b>Shipped without an implementation, and that is the point.</b> The brief asks for an
/// availability calendar as a future-ready abstraction, and a real one — per-date blocks, seasonal
/// rates, minimum stay by weekday, a booking ledger — is a booking engine, which is out of scope.
/// What is in scope is that nothing above the domain reads a date range off a listing directly:
/// callers ask <c>HousingAvailability.IsAvailableOn</c> and <c>AcceptsStay</c> today, and a
/// calendar module registers an implementation of this and answers the same two questions from a
/// table of dates tomorrow. Not one caller changes.
/// </para>
/// <para>
/// <b>On a deployment with no implementation the aggregate answers.</b> The optional dependency
/// stays null, the value object's window is authoritative, and a hostel that wants per-night
/// availability simply does not have it yet — which is an honest limitation rather than a broken
/// feature.
/// </para>
/// </summary>
public interface IHousingAvailabilityCalendar
{
    /// <summary>Whether the property can be moved into on a date.</summary>
    Task<bool> IsAvailableOnAsync(
        HousingListingId listingId,
        DateTimeOffset moment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many units are free across a whole span.
    /// <para>
    /// A span rather than a moment, because that is the question a real calendar exists to answer
    /// and the one the value object cannot: a hostel bed free on Tuesday and booked on Wednesday is
    /// not available for a two-night stay, and no single date range can say so.
    /// </para>
    /// </summary>
    Task<int> UnitsAvailableBetweenAsync(
        HousingListingId listingId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a span is taken. Called after a reservation commits, so a calendar stays in
    /// step with the unit counts without owning them.
    /// </summary>
    Task BlockAsync(
        HousingListingId listingId,
        DateTimeOffset from,
        DateTimeOffset until,
        int units,
        CancellationToken cancellationToken = default);

    /// <summary>Releases a span a cancelled or expired reservation had taken.</summary>
    Task ReleaseAsync(
        HousingListingId listingId,
        DateTimeOffset from,
        DateTimeOffset until,
        int units,
        CancellationToken cancellationToken = default);
}
