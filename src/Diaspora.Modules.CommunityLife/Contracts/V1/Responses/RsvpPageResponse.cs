namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A page of the caller's own responses, each with the event it belongs to.</summary>
public sealed record RsvpPageResponse
{
    public IReadOnlyList<RsvpResponse> Items { get; init; } = [];

    public IReadOnlyList<EventSummaryResponse> Events { get; init; } = [];

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
