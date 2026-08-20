namespace Diaspora.Modules.Guides.Domain.Knowledge;

/// <summary>
/// One knowledge category's name and description in one locale.
/// <para>
/// <b>Stored translations, not machine ones.</b> The brief puts automatic translation out of
/// scope, and that is the right call for navigation labels in particular: "Immigration" and
/// "Cost of Living" are terms a community uses about itself, and a machine rendering of them into
/// Kreol or Wolof is worse than the English a reader already recognises. What ships is a place for
/// a human translation to live.
/// </para>
/// <para>
/// Owned by the category rather than a table of its own, because a localization has no life
/// without the node it labels and nothing ever queries one without the other.
/// </para>
/// </summary>
public sealed class KnowledgeCategoryLocalization
{
    private KnowledgeCategoryLocalization(string locale, string name, string? description)
    {
        Locale = locale;
        Name = name;
        Description = description;
    }

    private KnowledgeCategoryLocalization() { }

    /// <summary>A normalized BCP-47 tag. Half of the row's identity.</summary>
    public string Locale { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>
    /// Builds a localization, or null when the locale or the name is unusable.
    /// <para>
    /// Null rather than a <c>Result</c>: the caller is the aggregate, which is replacing the whole
    /// set and already knows to reject the batch. A per-row <c>Result</c> here would be unwrapped
    /// at exactly one call site.
    /// </para>
    /// </summary>
    internal static KnowledgeCategoryLocalization? TryCreate(
        string? locale, string? name, string? description, int maxName, int maxDescription)
    {
        var normalizedLocale = KnowledgeNormalization.Locale(locale);
        var trimmedName = KnowledgeNormalization.Text(name, maxName);

        return normalizedLocale is null || trimmedName is null
            ? null
            : new KnowledgeCategoryLocalization(
                normalizedLocale,
                trimmedName,
                KnowledgeNormalization.Text(description, maxDescription));
    }
}
