namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One line of a country's category breakdown: how much is written, and how much it is read.
/// <para>
/// <b>Both numbers, because either alone is misleading.</b> A category with thirty guides and no
/// views is thirty guides nobody needed. A category with four guides and the highest view count on
/// the country is where the next twenty guides should go. Editorial planning is entirely about the
/// ratio, and a breakdown that reported only one half of it would be planning data that cannot be
/// planned from.
/// </para>
/// </summary>
public sealed record KnowledgeCategoryCountResponse
{
    public required string KnowledgeCategoryKey { get; init; }

    /// <summary>Already localized.</summary>
    public required string Name { get; init; }

    public string? Icon { get; init; }

    /// <summary>Published guides this row resolves to for the country.</summary>
    public required int GuideCount { get; init; }

    /// <summary>Times this row was opened on this country's directory.</summary>
    public required long Views { get; init; }
}
