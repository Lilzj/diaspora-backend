namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Rates a completed purchase.
/// <para>
/// The reservation is the subject, not the seller. That is what makes the star mean somebody
/// actually bought something — and it is why a repeat buyer rates each purchase separately
/// rather than overwriting what they said about the last one.
/// </para>
/// </summary>
public sealed record WriteSellerReviewRequest
{
    /// <summary>The completed reservation being reviewed.</summary>
    public required Guid ReservationId { get; init; }

    /// <summary>One to five.</summary>
    public required int Rating { get; init; }

    public string? Title { get; init; }

    public string? Body { get; init; }
}

/// <summary>The seller's one public reply to a review.</summary>
public sealed record RespondToSellerReviewRequest
{
    public required string Response { get; init; }
}
