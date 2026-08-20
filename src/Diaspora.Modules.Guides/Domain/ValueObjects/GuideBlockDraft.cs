using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// One content block as the editor submitted it — pure input, not an entity.
/// See <see cref="GuideSectionDraft"/> for why drafts and entities are separate types.
/// </summary>
public sealed record GuideBlockDraft
{
    public required Guid BlockId { get; init; }

    public required GuideBlockKind Kind { get; init; }

    public required int Order { get; init; }

    /// <summary>The prose, or the caption for a media-kind block.</summary>
    public string? Content { get; init; }

    /// <summary>The referenced asset, required for media-kind blocks.</summary>
    public Guid? MediaId { get; init; }

    /// <summary>Opaque JSON for table and link payloads. The editor owns the shape.</summary>
    public string? Metadata { get; init; }
}
