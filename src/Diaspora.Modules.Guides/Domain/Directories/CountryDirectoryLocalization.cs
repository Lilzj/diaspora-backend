using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Directories;

/// <summary>
/// One country directory's own name and standfirst in one locale.
/// <para>
/// The header of a country page — "Mauritius" and the paragraph under it — separately from the
/// category labels, because they are written by different people at different times: the header is
/// editorial copy for a launch, and the labels are catalogue vocabulary that outlives it.
/// </para>
/// </summary>
public sealed class CountryDirectoryLocalization
{
    private CountryDirectoryLocalization(string locale, string displayName, string? summary)
    {
        Locale = locale;
        DisplayName = displayName;
        Summary = summary;
    }

    private CountryDirectoryLocalization() { }

    /// <summary>A normalized BCP-47 tag. Half of the row's identity.</summary>
    public string Locale { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Summary { get; private set; }

    internal static CountryDirectoryLocalization? TryCreate(
        string? locale, string? displayName, string? summary)
    {
        var normalizedLocale = KnowledgeNormalization.Locale(locale);
        var name = KnowledgeNormalization.Text(displayName, KnowledgeLimits.MaxDirectoryName);

        return normalizedLocale is null || name is null
            ? null
            : new CountryDirectoryLocalization(
                normalizedLocale,
                name,
                KnowledgeNormalization.Text(summary, KnowledgeLimits.MaxDirectorySummary));
    }
}
