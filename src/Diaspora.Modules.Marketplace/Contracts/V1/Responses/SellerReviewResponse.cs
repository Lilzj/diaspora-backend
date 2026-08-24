namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>One published review of a seller.</summary>
public sealed record SellerReviewResponse
{
    public required Guid ReviewId { get; init; }

    public required Guid SellerId { get; init; }

    public required Guid ReviewerId { get; init; }

    /// <summary>
    /// The purchase this is about.
    /// <para>
    /// On the wire because it is the review's warrant: a client showing "verified purchase"
    /// beside a star is stating a fact the server checked, not a badge it invented.
    /// </para>
    /// </summary>
    public required Guid ReservationId { get; init; }

    public required Guid ListingId { get; init; }

    /// <summary>One to five.</summary>
    public required int Rating { get; init; }

    public string? Title { get; init; }

    public string? Body { get; init; }

    /// <summary>The seller's public reply, when they have given one.</summary>
    public string? Response { get; init; }

    public DateTimeOffset? RespondedAt { get; init; }

    /// <summary>Whether the caller may still change this review. Drives the edit affordance.</summary>
    public required bool CanEdit { get; init; }

    public DateTimeOffset? EditedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// A seller's rating rollup.
/// <para>
/// <b><see cref="Average"/> is meaningless unless <see cref="TotalReviews"/> is above zero.</b>
/// An unrated seller answers zero here, and zero is not a rating — the client that reads this
/// must render "no reviews yet" rather than a star, and must never substitute a default. The
/// marketplace screen did substitute one, hardcoded to 4.8, for every seller on the platform.
/// </para>
/// </summary>
public sealed record SellerRatingResponse
{
    /// <summary>One decimal place. Zero when there are no reviews.</summary>
    public required double Average { get; init; }

    public required int TotalReviews { get; init; }

    /// <summary>Bucket counts, one to five, for the proportional bars.</summary>
    public required IReadOnlyDictionary<string, int> Distribution { get; init; }
}

/// <summary>A page of a seller's reviews, with the rollup that belongs beside it.</summary>
public sealed record SellerReviewPageResponse
{
    public required SellerRatingResponse Rating { get; init; }

    public required IReadOnlyList<SellerReviewResponse> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
