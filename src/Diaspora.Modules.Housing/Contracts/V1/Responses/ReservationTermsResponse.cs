namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// What the parties say they agreed.
/// <para>
/// Recorded, never enforced, never interpreted. Lease management, payment processing and digital
/// signatures are all out of scope; this is the record a disagreement six weeks later turns on.
/// </para>
/// </summary>
public sealed record ReservationTermsResponse
{
    public DateTimeOffset? MoveInDate { get; init; }

    public DateTimeOffset? MoveOutDate { get; init; }

    /// <summary>When the hold lapses if nothing happens.</summary>
    public DateTimeOffset? HoldExpiresAt { get; init; }

    public decimal? AgreedRent { get; init; }

    public decimal? AgreedDeposit { get; init; }

    public string? CurrencyCode { get; init; }

    public string? RentPeriod { get; init; }

    public required string LeaseType { get; init; }

    public required int Occupants { get; init; }

    /// <summary>The intended stay, where both dates are known.</summary>
    public int? StayNights { get; init; }
}
