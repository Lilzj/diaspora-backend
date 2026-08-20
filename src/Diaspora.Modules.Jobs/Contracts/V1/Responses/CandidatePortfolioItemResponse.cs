namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>One piece of a candidate's portfolio — a link or an uploaded sample.</summary>
public sealed record CandidatePortfolioItemResponse
{
    public required Guid ItemId { get; init; }

    public required string Label { get; init; }

    /// <summary>An absolute http(s) link, validated on the way in.</summary>
    public string? Url { get; init; }

    /// <summary>A Media reference to an uploaded sample.</summary>
    public Guid? MediaId { get; init; }

    public string? Description { get; init; }
}
