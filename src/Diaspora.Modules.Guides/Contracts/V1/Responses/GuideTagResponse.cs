namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One tag and how many published guides carry it. What a chip cloud renders.</summary>
public sealed record GuideTagResponse
{
    /// <summary>The normalized slug.</summary>
    public required string Tag { get; init; }

    public required int Count { get; init; }
}
