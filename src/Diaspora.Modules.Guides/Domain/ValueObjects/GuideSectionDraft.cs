namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// One section as the editor submitted it — pure input, not an entity.
/// <para>
/// The full-document save (<c>Guide.ReplaceStructure</c>) takes a list of these and rebuilds
/// the owned graph from them. A plain record rather than the entity itself, so the request
/// pipeline never constructs half-valid entities: everything here is unvalidated author input
/// until the aggregate says otherwise. Identifiers are supplied by the caller — ids are never
/// generated inside domain code.
/// </para>
/// </summary>
public sealed record GuideSectionDraft
{
    public required Guid SectionId { get; init; }

    public required string? Title { get; init; }

    public required int Order { get; init; }

    public IReadOnlyList<GuideBlockDraft> Blocks { get; init; } = [];
}
