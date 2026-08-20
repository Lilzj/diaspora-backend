using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// One official notice in one language.
/// <para>
/// <b>A human translation, never a machine one.</b> The brief puts automatic translation out of
/// scope, and on official text that is not a limitation but a requirement: a machine rendering of
/// "applicants must present the original" that drops the word "original" is a mistranslation
/// somebody acts on at a counter. What ships is a place for a translation somebody is accountable
/// for.
/// </para>
/// <para>
/// <b>All three fields together, or the row is refused.</b> A locale carrying a translated title
/// over an untranslated body is the worst of both: a reader picks it because the heading is in their
/// language and then cannot read the instruction. Falling back to the default language wholesale is
/// honest; falling back halfway is not.
/// </para>
/// </summary>
public sealed class NoticeLocalization
{
    private NoticeLocalization(string locale, string title, string summary, string content)
    {
        Locale = locale;
        Title = title;
        Summary = summary;
        Content = content;
    }

    private NoticeLocalization() { }

    /// <summary>A normalized BCP-47 tag. Half of the row's identity.</summary>
    public string Locale { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Builds a translation, or null when the locale is unusable or any of the three parts is
    /// missing. Null rather than a <c>Result</c>: the caller is the aggregate, which replaces the
    /// whole set and already knows to reject the batch.
    /// </summary>
    internal static NoticeLocalization? TryCreate(
        string? locale, string? title, string? summary, string? content)
    {
        var normalizedLocale = IntelligenceNormalization.Locale(locale);
        var trimmedTitle = IntelligenceNormalization.Text(title, IntelligenceLimits.MaxNoticeTitle);
        var trimmedSummary = IntelligenceNormalization.Text(
            summary, IntelligenceLimits.MaxNoticeSummary);
        var trimmedContent = IntelligenceNormalization.Text(
            content, IntelligenceLimits.MaxNoticeContent);

        return normalizedLocale is null
            || trimmedTitle is null
            || trimmedSummary is null
            || trimmedContent is null
                ? null
                : new NoticeLocalization(
                    normalizedLocale, trimmedTitle, trimmedSummary, trimmedContent);
    }
}
