using System.Text.Json.Serialization;

namespace Diaspora.Modules.Guides.Application.Services.Snapshots;

/// <summary>
/// The serialized form of a guide's section structure — what a <c>GuideVersion</c> stores and
/// a draft auto-save keeps.
/// <para>
/// Property order is pinned with <see cref="JsonPropertyOrderAttribute"/> on every member,
/// because the output has to be byte-stable across compilers and runtimes: version snapshots
/// are compared and archived, and the B1.2 lesson is that reflection order is an accident that
/// changes out from under you. The schema version is first, so a future shape change can be
/// read before the reader commits to a shape.
/// </para>
/// </summary>
public sealed record GuideSnapshot
{
    /// <summary>The shape of this document. Bumped only by a deliberate migration.</summary>
    [JsonPropertyOrder(0)]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyOrder(1)]
    public IReadOnlyList<GuideSectionSnapshot> Sections { get; init; } = [];
}
