namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>One row of the home feed on the wire.</summary>
public sealed record FeedSectionShape
{
    public string? Key { get; init; }

    public string? Title { get; init; }

    public string? TitleResourceKey { get; init; }

    public string? TargetType { get; init; }

    public List<RecommendationShape> Items { get; init; } = [];

    public string? SeeAllRoute { get; init; }

    public bool IsColdStart { get; init; }
}
