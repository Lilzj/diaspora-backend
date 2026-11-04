namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// What Trust has said about the guide's author, from this module's local projection. Rendered
/// beside the byline so a reader can weigh the advice; fed only by Trust integration events.
/// </summary>
public sealed record GuideAuthorResponse
{
    public required Guid UserId { get; init; }

    /// <summary>
    /// The author's display name, resolved from Identity. Null when it could not be resolved.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>The author's profile photo, an address an image can load. Null when they have none.</summary>
    public string? AvatarUrl { get; init; }

    public required bool IsApprovedContributor { get; init; }

    public required bool IsCommunityGuide { get; init; }

    /// <summary>Trust's level label, opaque to this module. Rendered, never interpreted.</summary>
    public string? TrustLevel { get; init; }

    public IReadOnlyList<GuideAuthorBadgeResponse> Badges { get; init; } = [];
}
