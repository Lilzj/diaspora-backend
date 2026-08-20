namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Attaches a media asset to the guide, by reference — never a URL, per ADR-005. Idempotent on
/// (media, role), which is what makes the endpoint safely retryable.
/// </summary>
public sealed record AttachGuideMediaRequest
{
    public required Guid MediaId { get; init; }

    /// <summary>A <c>GuideMediaRole</c> name — <c>Hero</c>, <c>Cover</c>, <c>Gallery</c>…</summary>
    public required string Role { get; init; }

    public string? Caption { get; init; }
}
