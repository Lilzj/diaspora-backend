namespace Diaspora.Modules.Guides.Extensibility.Caching;

/// <summary>
/// The cache key vocabulary for country intelligence.
/// <para>
/// <b>A separate prefix from B8.1's, sharing the same cache.</b> The abstraction is reused — one
/// cache seam per module, not one per phase, because a deployment configuring Redis should not have
/// to do it twice — but the key spaces are distinct so that publishing a notice drops the
/// intelligence page for a country without dropping its guide dashboard, which nothing about the
/// notice changed.
/// </para>
/// <para>
/// <b>The locale is part of every key.</b> A country page cached without it serves the first
/// reader's language to everybody, and the resulting bug — a French speaker seeing English labels
/// only sometimes, depending on who asked first — is very hard to reproduce and very easy to prevent.
/// </para>
/// </summary>
public static class IntelligenceCacheKeys
{
    /// <summary>Everything under one country. The unit of invalidation on any editorial change.</summary>
    public const string CountryPrefix = "intelligence:country:";

    /// <summary>
    /// The composed intelligence page for one country and locale.
    /// <para>
    /// The only key in this space, deliberately. The dashboard <em>band</em> is not cached here — it
    /// is composed inside B8.1's country dashboard, which has its own cache and its own invalidation,
    /// and a second cached copy of the same three notices would be a second thing to invalidate and a
    /// second chance to forget.
    /// </para>
    /// </summary>
    public static string Country(string countryCode, string? locale) =>
        $"{CountryPrefix}{countryCode}:page:{Locale(locale)}";

    /// <summary>The prefix covering everything cached for one country's intelligence.</summary>
    public static string CountryScope(string countryCode) => $"{CountryPrefix}{countryCode}:";

    /// <summary>
    /// The locale segment. Absent locales collapse to one bucket rather than to the empty string, so
    /// a key never ends in a separator and two adjacent segments can never run together.
    /// </summary>
    private static string Locale(string? locale) =>
        string.IsNullOrWhiteSpace(locale) ? "default" : locale;
}
