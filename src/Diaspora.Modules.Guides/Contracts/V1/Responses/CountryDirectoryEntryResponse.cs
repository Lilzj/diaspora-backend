namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One row on one country's directory — the unit the country page renders as a tappable line.
/// <para>
/// <see cref="Label"/> is the country's own wording where a curator set one and the catalogue's
/// otherwise, already resolved. <see cref="GuideCount"/> is what turns a menu into a directory: a
/// reader deciding where to start is guided far more by "Housing — 14 guides" than by "Housing",
/// and an empty row tells them honestly that this is not where the answers are yet.
/// </para>
/// </summary>
public sealed record CountryDirectoryEntryResponse
{
    /// <summary>The catalogue key. What the category endpoint is addressed by.</summary>
    public required string KnowledgeCategoryKey { get; init; }

    /// <summary>Already resolved: the country's override, or the localized catalogue name.</summary>
    public required string Label { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public required int Position { get; init; }

    /// <summary>Published guides this row currently resolves to, for this country.</summary>
    public required int GuideCount { get; init; }

    /// <summary>
    /// Whether the row is arranged but not shown. Always false on a reader-facing response —
    /// hidden rows are filtered out before the response is built — and meaningful to a curator.
    /// </summary>
    public required bool IsHidden { get; init; }
}
