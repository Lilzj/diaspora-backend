namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>One hashtag.</summary>
public sealed record HashtagResponse
{
    /// <summary>The canonical form. What a feed filters on.</summary>
    public required string Slug { get; init; }

    /// <summary>The capitalisation the first member used. What a client shows.</summary>
    public required string DisplayText { get; init; }

    public required int UsageCount { get; init; }

    /// <summary>Uses inside the current trending window.</summary>
    public required int RecentUsageCount { get; init; }

    public string? PrimaryCountryCode { get; init; }
}
