namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>The home feed on the wire.</summary>
public sealed record HomeFeedShape
{
    public List<FeedSectionShape> Sections { get; init; } = [];

    /// <summary>False when the member turned personalization off. What the client surfaces.</summary>
    public bool IsPersonalized { get; init; }

    public bool IsColdStart { get; init; }

    public int ProfileCompleteness { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }
}
