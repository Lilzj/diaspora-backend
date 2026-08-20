namespace Diaspora.Modules.Guides.Integration;

/// <summary>
/// One category, described in terms every consuming module can read. Flat primitives, for the
/// same reason <see cref="GuideSummary"/> is: a consumer reading a category must not take a
/// dependency on how B8 stores a catalogue.
/// </summary>
public sealed record GuideCategoryDescriptor
{
    /// <summary>The stable key guides store and URLs carry. The identity that survives a reseed.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An emoji or icon name. Opaque — the client decides what it is.</summary>
    public string? Icon { get; init; }

    public required int Order { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>The parent's key, for a shallow hierarchy. Null at the root.</summary>
    public string? ParentKey { get; init; }
}
