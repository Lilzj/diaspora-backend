namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Takes a hold on a property.
/// <para>
/// The units come off the listing in the same transaction, so a reservation never exists without
/// its hold. Where the landlord requires a viewing first, one must already be completed.
/// </para>
/// </summary>
public sealed record CreateHousingReservationRequest
{
    /// <summary>How many units. One for a home, more for hostel beds.</summary>
    public int Units { get; init; } = 1;

    public DateTimeOffset? MoveInDate { get; init; }

    /// <summary>Null for an open-ended tenancy.</summary>
    public DateTimeOffset? MoveOutDate { get; init; }

    /// <summary>How many people will live there. Checked against the property maximum.</summary>
    public int Occupants { get; init; } = 1;

    /// <summary>What the parties say they agreed. Recorded, never enforced.</summary>
    public decimal? AgreedRent { get; init; }

    public decimal? AgreedDeposit { get; init; }

    public string? CurrencyCode { get; init; }

    public string? RentPeriod { get; init; }

    public string? LeaseType { get; init; }

    /// <summary>The viewing this followed from, where there was one.</summary>
    public Guid? ViewingId { get; init; }

    public string? Message { get; init; }
}
