namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Restates when a property is available and how many units it has.
/// <para>
/// The unit count is refused below what is already reserved or occupied: a hostel cannot shrink to
/// four beds while five people are booked into them.
/// </para>
/// </summary>
public sealed record SetAvailabilityRequest
{
    public DateTimeOffset? AvailableFrom { get; init; }

    public DateTimeOffset? AvailableUntil { get; init; }

    public int? TotalUnits { get; init; }

    public int? MinimumStayNights { get; init; }

    public int? MaximumStayNights { get; init; }

    public int? BaseRevision { get; init; }
}
