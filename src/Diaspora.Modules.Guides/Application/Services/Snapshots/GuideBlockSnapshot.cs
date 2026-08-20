using System.Text.Json.Serialization;

namespace Diaspora.Modules.Guides.Application.Services.Snapshots;

/// <summary>
/// One block as it stood at a snapshot. The kind is a string, not the enum — a snapshot
/// written today must deserialize in five years, whatever the enum's numbering has become.
/// </summary>
public sealed record GuideBlockSnapshot
{
    [JsonPropertyOrder(0)]
    public required Guid BlockId { get; init; }

    /// <summary>A <c>GuideBlockKind</c> name, stored as text for longevity.</summary>
    [JsonPropertyOrder(1)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(2)]
    public required int Order { get; init; }

    [JsonPropertyOrder(3)]
    public string? Content { get; init; }

    /// <summary>A Media identifier. Never a URL — the reference outlives storage migrations.</summary>
    [JsonPropertyOrder(4)]
    public Guid? MediaId { get; init; }

    /// <summary>Opaque JSON for table and link payloads, preserved verbatim.</summary>
    [JsonPropertyOrder(5)]
    public string? Metadata { get; init; }
}
