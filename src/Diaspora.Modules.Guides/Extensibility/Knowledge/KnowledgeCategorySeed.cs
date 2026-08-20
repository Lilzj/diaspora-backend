namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// One navigation row a module contributes to the Knowledge Directory's catalogue.
/// <para>
/// <b>A seed, not a category.</b> It is a request to create a node if one with that key does not
/// exist, and it says nothing at all about a node that does — the seeder never renames, reorders
/// or re-describes an existing key, because staff edit these in production and a seeder that
/// overwrote their work every deploy would quietly undo it every release. B8 records the same rule
/// for its own catalogue.
/// </para>
/// </summary>
public sealed record KnowledgeCategorySeed
{
    /// <summary>The stable key. Lower-case kebab, unique across the catalogue.</summary>
    public required string Key { get; init; }

    /// <summary>The default label, in the platform's own language.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An emoji or icon name. Opaque — the client decides what it is.</summary>
    public string? Icon { get; init; }

    /// <summary>The catalogue-wide default position. A country directory may override it.</summary>
    public int Order { get; init; }

    /// <summary>
    /// The guide categories this row draws from. Empty means the guide category whose key matches
    /// this one, which is the common case and needs no rows at all.
    /// </summary>
    public IReadOnlyList<string> SourceCategoryKeys { get; init; } = [];

    /// <summary>
    /// Provider service categories to recommend beside this subject.
    /// <para>
    /// Keys only. B8.1 never resolves a provider, ranks one or vouches for one — B7 owns all
    /// three, and these keys are what a country page hands it to ask.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> ProviderCategoryKeys { get; init; } = [];

    /// <summary>Human translations of the label, keyed by BCP-47 locale tag.</summary>
    public IReadOnlyDictionary<string, string> Localizations { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
