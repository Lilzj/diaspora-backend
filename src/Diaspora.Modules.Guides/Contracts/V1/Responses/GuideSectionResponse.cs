namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One titled chapter of the guide, in reading order.</summary>
public sealed record GuideSectionResponse
{
    public required Guid SectionId { get; init; }

    public required string Title { get; init; }

    public required int Order { get; init; }

    public IReadOnlyList<GuideBlockResponse> Blocks { get; init; } = [];
}
