using System.Text.Json.Serialization;

namespace Diaspora.Modules.Guides.Application.Services.Snapshots;

/// <summary>
/// One section as it stood at a snapshot. The identifier is the section's stable identity —
/// it is what the version diff matches on, so "renamed" and "replaced" stay distinguishable.
/// </summary>
public sealed record GuideSectionSnapshot
{
    [JsonPropertyOrder(0)]
    public required Guid SectionId { get; init; }

    [JsonPropertyOrder(1)]
    public required string Title { get; init; }

    [JsonPropertyOrder(2)]
    public required int Order { get; init; }

    [JsonPropertyOrder(3)]
    public IReadOnlyList<GuideBlockSnapshot> Blocks { get; init; } = [];
}
