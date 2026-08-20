namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// An automated review. The client's AI review.
/// <para>
/// <c>status</c> may be <c>unavailable</c>, which the client does not currently model and which
/// it must not treat as clean: it means no provider answered.
/// </para>
/// </summary>
public sealed record ContentReviewResponse
{
    public required Guid ReviewId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required string Status { get; init; }

    public required IReadOnlyList<string> Flags { get; init; }

    public required double Confidence { get; init; }

    /// <summary>Which provider answered, and which version of it. For appeals.</summary>
    public required string Provider { get; init; }

    public string? ProviderVersion { get; init; }

    public required DateTimeOffset ProcessedAt { get; init; }
}
