using System.Globalization;

namespace Diaspora.Modules.Guides.Extensibility.Caching;

/// <summary>
/// The cache key vocabulary, in one place because invalidation depends on it.
/// <para>
/// <b>A cache is only as good as its invalidation, and invalidation is only possible if the keys
/// have a predictable shape.</b> Every key here begins with a prefix that names what would have to
/// change for it to be stale — the country, or the catalogue. When a directory is arranged, every
/// key under that country's prefix is dropped; when a category is renamed, the catalogue prefix is.
/// Keys assembled ad hoc at call sites cannot be invalidated as a group, which is how a caching
/// layer becomes a source of wrong answers rather than fast ones.
/// </para>
/// <para>
/// <b>The locale is part of every reader-facing key.</b> A directory cached without it serves the
/// first reader's language to everybody, and the resulting bug — a French speaker seeing English
/// labels only sometimes, depending on who asked first — is very hard to reproduce and very easy to
/// prevent.
/// </para>
/// </summary>
public static class KnowledgeCacheKeys
{
    /// <summary>Everything under one country. The unit of invalidation on any curation change.</summary>
    public const string CountryPrefix = "knowledge:country:";

    /// <summary>The navigation catalogue, which is country-independent.</summary>
    public const string CataloguePrefix = "knowledge:catalogue:";

    /// <summary>Everything about one guide — its next reads, mainly.</summary>
    public const string GuidePrefix = "knowledge:guide:";

    public static string Directory(string countryCode, string? locale) =>
        $"{CountryPrefix}{countryCode}:directory:{Locale(locale)}";

    public static string Dashboard(string countryCode, string? locale) =>
        $"{CountryPrefix}{countryCode}:dashboard:{Locale(locale)}";

    public static string Category(string countryCode, string categoryKey, string? locale) =>
        $"{CountryPrefix}{countryCode}:category:{categoryKey}:{Locale(locale)}";

    public static string Statistics(string countryCode) =>
        $"{CountryPrefix}{countryCode}:statistics";

    public static string LearningPaths(string countryCode, bool publishedOnly) =>
        $"{CountryPrefix}{countryCode}:paths:{(publishedOnly ? "published" : "all")}";

    public static string Catalogue(bool includeArchived, string? locale) =>
        $"{CataloguePrefix}{(includeArchived ? "all" : "active")}:{Locale(locale)}";

    public static string Countries(bool publishedOnly) =>
        $"{CataloguePrefix}countries:{(publishedOnly ? "published" : "all")}";

    public static string RecommendedReads(Guid guideId, int limit) =>
        $"{GuidePrefix}{guideId:N}:next:{limit.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The prefix covering everything cached for one country.</summary>
    public static string CountryScope(string countryCode) => $"{CountryPrefix}{countryCode}:";

    /// <summary>The prefix covering everything cached for one guide.</summary>
    public static string GuideScope(Guid guideId) => $"{GuidePrefix}{guideId:N}:";

    /// <summary>
    /// The locale segment. Absent locales collapse to one bucket rather than to the empty string,
    /// so a key never ends in a separator and two adjacent segments can never run together.
    /// </summary>
    private static string Locale(string? locale) =>
        string.IsNullOrWhiteSpace(locale) ? "default" : locale;
}
