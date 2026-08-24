namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Rates a tenancy.
/// <para>
/// The reservation is the subject, not the landlord. That is what makes the star mean somebody
/// actually moved in — and it is why a returning tenant rates each tenancy separately rather
/// than overwriting what they said about the last one.
/// </para>
/// </summary>
public sealed record WriteLandlordReviewRequest
{
    /// <summary>The reservation, which must have reached <c>Occupied</c>.</summary>
    public required Guid ReservationId { get; init; }

    /// <summary>One to five.</summary>
    public required int Rating { get; init; }

    public string? Title { get; init; }

    public string? Body { get; init; }
}

/// <summary>The landlord's one public reply to a review.</summary>
public sealed record RespondToLandlordReviewRequest
{
    public required string Response { get; init; }
}
