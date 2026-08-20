namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// What Trust has said about the guide's author, from this module's local projection. Rendered
/// beside the byline so a reader can weigh the advice; fed only by Trust integration events.
/// </summary>
public sealed record GuideAuthorResponse
{
    public required Guid UserId { get; init; }

    public required bool IsApprovedContributor { get; init; }

    public required bool IsCommunityGuide { get; init; }

    /// <summary>Trust's level label, opaque to this module. Rendered, never interpreted.</summary>
    public string? TrustLevel { get; init; }

    public IReadOnlyList<GuideAuthorBadgeResponse> Badges { get; init; } = [];
}
