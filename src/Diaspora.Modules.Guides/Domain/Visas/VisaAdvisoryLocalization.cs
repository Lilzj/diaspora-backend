using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Visas;

/// <summary>
/// One visa advisory's title, summary and body in one locale.
/// <para>
/// <b>The requirement lines are deliberately not translated here.</b> They are a list whose order and
/// membership carry meaning, and a translation set that could add or drop a line would be able to
/// tell a reader in one language that they need a document readers in another are not told about. A
/// translated advisory carries translated framing over the authoritative list; a deployment that
/// needs the lines themselves translated writes a second advisory, which is also what a ministry
/// publishing in two languages actually does.
/// </para>
/// <para>
/// All three fields together or the row is refused, on the same reasoning
/// <see cref="Intelligence.NoticeLocalization"/> gives: a translated heading over an untranslated
/// instruction is worse than no translation, because a reader picks it and then cannot read the part
/// that matters.
/// </para>
/// </summary>
public sealed class VisaAdvisoryLocalization
{
    private VisaAdvisoryLocalization(string locale, string title, string summary, string content)
    {
        Locale = locale;
        Title = title;
        Summary = summary;
        Content = content;
    }

    private VisaAdvisoryLocalization() { }

    /// <summary>A normalized BCP-47 tag. Half of the row's identity.</summary>
    public string Locale { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    internal static VisaAdvisoryLocalization? TryCreate(
        string? locale, string? title, string? summary, string? content)
    {
        var normalizedLocale = IntelligenceNormalization.Locale(locale);
        var trimmedTitle = IntelligenceNormalization.Text(title, IntelligenceLimits.MaxVisaTitle);
        var trimmedSummary = IntelligenceNormalization.Text(
            summary, IntelligenceLimits.MaxVisaSummary);
        var trimmedContent = IntelligenceNormalization.Text(
            content, IntelligenceLimits.MaxVisaContent);

        return normalizedLocale is null
            || trimmedTitle is null
            || trimmedSummary is null
            || trimmedContent is null
                ? null
                : new VisaAdvisoryLocalization(
                    normalizedLocale, trimmedTitle, trimmedSummary, trimmedContent);
    }
}
