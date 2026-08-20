namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// The arrangement a brand-new country directory starts from.
/// <para>
/// <b>Launching a country should not begin with an empty page.</b> A curator opening a new
/// directory to twenty-three unordered rows and a blank ordering will spend an afternoon
/// reproducing an arrangement that is roughly the same for every country — because it is: people
/// arriving anywhere need to get oriented, then sort out their right to be there, then somewhere to
/// live, then money, then work. The template encodes that, and the curator edits from it.
/// </para>
/// <para>
/// <b>It is a starting point and never a constraint.</b> The template is applied once, at creation,
/// and the directory is a plain arrangement from that moment on. Nothing re-applies it, so a
/// curator who moves Student Life to the top for a university town keeps it there forever.
/// </para>
/// </summary>
public sealed record CountryDirectoryTemplate
{
    /// <summary>
    /// The country this template is for, or null for the default that applies to any country.
    /// <para>
    /// A country-specific template wins over the default. That is how a deployment serving one
    /// diaspora particularly well ships its own opinionated arrangement without imposing it on
    /// every other country in the catalogue.
    /// </para>
    /// </summary>
    public string? CountryCode { get; init; }

    /// <summary>Lower wins when two templates could both apply.</summary>
    public int Order { get; init; }

    /// <summary>The catalogue keys to arrange, in the order they should appear.</summary>
    public required IReadOnlyList<string> KnowledgeCategoryKeys { get; init; }

    /// <summary>
    /// Keys to arrange but leave hidden — rows a curator will want, in the right place, once
    /// somebody has written guides for them.
    /// </summary>
    public IReadOnlyList<string> HiddenKeys { get; init; } = [];
}
