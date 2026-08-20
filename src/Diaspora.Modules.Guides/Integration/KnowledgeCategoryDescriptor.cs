namespace Diaspora.Modules.Guides.Integration;

/// <summary>
/// One navigation row, described for a consuming module.
/// <para>
/// Published separately from <see cref="GuideCategoryDescriptor"/> because the two are genuinely
/// different things, and a consumer that confused them would file its own content under a
/// navigation label that no author ever chose. B8's descriptor is the filing taxonomy — where a
/// guide goes. This is the directory's navigation — what a country page shows.
/// </para>
/// </summary>
public sealed record KnowledgeCategoryDescriptor
{
    /// <summary>The stable key. Survives a reseed and a rename.</summary>
    public required string Key { get; init; }

    /// <summary>Localized where the caller asked for a locale.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An emoji or icon name. Opaque — the consumer decides what it is.</summary>
    public string? Icon { get; init; }

    public required int Order { get; init; }

    /// <summary>Published guides this row resolves to, for the country asked about.</summary>
    public required int GuideCount { get; init; }

    /// <summary>
    /// Provider service categories this subject calls for, as B7 keys.
    /// <para>
    /// On the descriptor because it is what makes contextual provider recommendations possible for
    /// a consumer that is not B8.1: a Housing screen showing a neighbourhood guide can ask which
    /// kind of professional the subject calls for, and hand the keys to B7.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> ProviderCategoryKeys { get; init; } = [];
}
