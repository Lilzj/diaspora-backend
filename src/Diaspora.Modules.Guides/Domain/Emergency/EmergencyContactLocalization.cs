using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Emergency;

/// <summary>
/// One emergency contact's label and note in one locale.
/// <para>
/// <b>Owned by the directory rather than by the contact, and keyed by (locale, kind).</b> Nesting an
/// owned collection inside an owned collection is a shape EF maps and nobody enjoys reading — and it
/// buys nothing here, because the natural identity of a translation is the pair anyway. Flattening it
/// means one table, one composite key, and a read that resolves every translation for a country in a
/// single include.
/// </para>
/// <para>
/// <b>The number is deliberately absent.</b> A translation translates words; it must never be able
/// to change which number a frightened person dials. Anything that could vary the number belongs on
/// the contact itself, where one editor owns it.
/// </para>
/// </summary>
public sealed class EmergencyContactLocalization
{
    private EmergencyContactLocalization(string locale, string kind, string label, string? note)
    {
        Locale = locale;
        Kind = kind;
        Label = label;
        Note = note;
    }

    private EmergencyContactLocalization() { }

    /// <summary>A normalized BCP-47 tag. One third of the row's identity.</summary>
    public string Locale { get; private set; } = string.Empty;

    /// <summary>The contact kind this translates. The other third.</summary>
    public string Kind { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    internal static EmergencyContactLocalization? TryCreate(
        string? locale, string? kind, string? label, string? note)
    {
        var normalizedLocale = IntelligenceNormalization.Locale(locale);
        var normalizedKind = IntelligenceNormalization.Key(kind);
        var trimmedLabel = IntelligenceNormalization.Text(
            label, IntelligenceLimits.MaxEmergencyLabel);

        return normalizedLocale is null || normalizedKind is null || trimmedLabel is null
            ? null
            : new EmergencyContactLocalization(
                normalizedLocale,
                normalizedKind,
                trimmedLabel,
                IntelligenceNormalization.Text(note, IntelligenceLimits.MaxEmergencyNote));
    }
}
