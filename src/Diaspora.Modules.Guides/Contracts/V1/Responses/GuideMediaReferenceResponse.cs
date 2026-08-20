namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One media asset attached to the guide, by reference (ADR-005).</summary>
public sealed record GuideMediaReferenceResponse
{
    /// <summary>A Media identifier. Never a URL.</summary>
    public required Guid MediaId { get; init; }

    /// <summary>A <c>GuideMediaRole</c> name — <c>Hero</c>, <c>Cover</c>, <c>Gallery</c>…</summary>
    public required string Role { get; init; }

    public required int Order { get; init; }

    public string? Caption { get; init; }
}
