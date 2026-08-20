namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A page of reports, for the moderation queue.</summary>
public sealed record ReportPageResponse
{
    public IReadOnlyList<ReportResponse> Items { get; init; } = [];

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
