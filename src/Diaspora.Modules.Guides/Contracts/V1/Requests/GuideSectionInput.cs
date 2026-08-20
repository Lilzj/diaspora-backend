namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// One section as the editor submits it.
/// <para>
/// The identifier is supplied by the client and stable across saves — it is what version diffs
/// match sections by, so "renamed" and "replaced" stay distinguishable years later.
/// </para>
/// </summary>
public sealed record GuideSectionInput
{
    public required Guid SectionId { get; init; }

    public required string Title { get; init; }

    public required int Order { get; init; }

    public IReadOnlyList<GuideBlockInput> Blocks { get; init; } = [];
}
