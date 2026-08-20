using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// One notice category a contributor wants to exist.
/// <para>
/// A plain record rather than the aggregate, so a contributor can be written in an assembly that has
/// no reason to reference this module's domain — the same shape B8.1's category seed uses. Seeding is
/// idempotent on <see cref="Key"/>: a row whose key already exists is left entirely alone, because
/// staff edit these labels in production and a seeder that overwrote them would undo that work every
/// release.
/// </para>
/// </summary>
public sealed record NoticeCategorySeed
{
    /// <summary>Lower-case kebab. The stable identity, and what a member's preference stores.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An emoji or icon name. Opaque to the server.</summary>
    public string? Icon { get; init; }

    /// <summary>Catalogue-wide position. Lower first.</summary>
    public int Order { get; init; }

    /// <summary>What a notice in this category is worth before an editor decides otherwise.</summary>
    public NoticeSeverity DefaultSeverity { get; init; } = NoticeSeverity.Informational;

    /// <summary>Which extra published contract this category's notices produce.</summary>
    public NoticeAlertFamily AlertFamily { get; init; } = NoticeAlertFamily.None;

    /// <summary>Locale to label. Human translations only.</summary>
    public IReadOnlyDictionary<string, string> Localizations { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
