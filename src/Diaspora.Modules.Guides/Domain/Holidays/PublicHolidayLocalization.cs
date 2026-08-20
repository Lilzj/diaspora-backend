using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Holidays;

/// <summary>
/// A public holiday's name and description in one locale.
/// <para>
/// The field that most needs translating in this whole phase, and the reason is that holiday names
/// are not translated so much as they are <em>different</em>: Thaipusam, Eid al-Fitr, Toussaint and
/// Ganesh Chaturthi are each written differently by each community that observes them, and an editor
/// picking one spelling makes the calendar read as belonging to one of them.
/// </para>
/// </summary>
public sealed class PublicHolidayLocalization
{
    private PublicHolidayLocalization(string locale, string name, string? description)
    {
        Locale = locale;
        Name = name;
        Description = description;
    }

    private PublicHolidayLocalization() { }

    /// <summary>A normalized BCP-47 tag. Half of the row's identity.</summary>
    public string Locale { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    internal static PublicHolidayLocalization? TryCreate(
        string? locale, string? name, string? description)
    {
        var normalizedLocale = IntelligenceNormalization.Locale(locale);
        var trimmedName = IntelligenceNormalization.Text(name, IntelligenceLimits.MaxHolidayName);

        return normalizedLocale is null || trimmedName is null
            ? null
            : new PublicHolidayLocalization(
                normalizedLocale,
                trimmedName,
                IntelligenceNormalization.Text(
                    description, IntelligenceLimits.MaxHolidayDescription));
    }
}
