namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A page of memberships.</summary>
public sealed record MembershipPageResponse
{
    public IReadOnlyList<MembershipResponse> Items { get; init; } = [];

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
