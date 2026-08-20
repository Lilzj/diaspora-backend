namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A seller's track record.
/// <para>
/// <b>Counts and elapsed times, and deliberately nothing that looks like a star rating.</b> A
/// rating is a review, reviews belong to Community, and a number here that a buyer read as one
/// would be this module quietly duplicating a capability another owns.
/// </para>
/// </summary>
public sealed record SellerStatisticsResponse
{
    public required int PublishedListings { get; init; }

    public required int CompletedSales { get; init; }

    public required int CompletedReservations { get; init; }

    /// <summary>Reservations the seller called off. The number a buyer most deserves to see.</summary>
    public required int CancelledReservations { get; init; }

    public required int AcceptedOffers { get; init; }

    /// <summary>Null below five finished reservations, where a rate would be noise.</summary>
    public double? CancellationRate { get; init; }

    /// <summary>Mean minutes to a first answer. Null until they have answered anything.</summary>
    public double? AverageResponseMinutes { get; init; }
}
