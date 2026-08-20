namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A page of repeating events.</summary>
public sealed record EventSeriesPageResponse
{
    public IReadOnlyList<EventSeriesResponse> Items { get; init; } = [];

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
