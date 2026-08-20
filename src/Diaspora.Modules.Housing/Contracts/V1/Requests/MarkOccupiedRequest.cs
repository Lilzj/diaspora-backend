namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Records that somebody has moved in, without going through a reservation.
/// <para>
/// The path for a landlord who let the property offline, which is common and must not be made
/// awkward: a platform that could only record lets it brokered would show a stale market.
/// </para>
/// </summary>
public sealed record MarkOccupiedRequest
{
    public int Units { get; init; } = 1;

    /// <summary>The member who moved in, where they are on the platform. Optional.</summary>
    public Guid? OccupantUserId { get; init; }

    /// <summary>What it actually let for. Feeds the neighbourhood market statistics.</summary>
    public decimal? LetAtAmount { get; init; }

    public string? CurrencyCode { get; init; }

    public string? RentPeriod { get; init; }
}
