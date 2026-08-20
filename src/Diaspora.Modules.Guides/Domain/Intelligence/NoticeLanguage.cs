namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// A language the authority officially issued this notice in.
/// <para>
/// <b>A different question from <see cref="NoticeLocalization"/>, and conflating the two loses the
/// answer to both.</b> A localization is a translation this platform holds. A language here is a
/// language the <em>issuing authority</em> published in — which is what decides whether a member can
/// go to the authority's own site and find text they can read, and which version a lawyer would cite.
/// A Mauritian circular issued in English and French, of which we hold a Kreol translation, has two
/// languages and three localizations, and every one of those numbers is true.
/// </para>
/// </summary>
public sealed class NoticeLanguage
{
    private NoticeLanguage(string code) => Code = code;

    private NoticeLanguage() { }

    /// <summary>A normalized BCP-47 tag. The row's identity within its notice.</summary>
    public string Code { get; private set; } = string.Empty;

    internal static NoticeLanguage? TryCreate(string? code)
    {
        var normalized = IntelligenceNormalization.Locale(code);

        return normalized is null ? null : new NoticeLanguage(normalized);
    }
}
