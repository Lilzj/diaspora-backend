namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A B8 guide linked beside an event or an organization.</summary>
public sealed record GuideReferenceResponse
{
    public required Guid GuideId { get; init; }

    public string? Title { get; init; }

    public string? Slug { get; init; }
}
