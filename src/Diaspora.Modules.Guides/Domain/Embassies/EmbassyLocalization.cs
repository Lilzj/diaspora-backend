using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Embassies;

/// <summary>
/// A mission's name, description and address in one locale.
/// <para>
/// <b>The address is translated too, and it is the field that most needed it.</b> A member reading
/// in French who is handed an English transliteration of a street in a country whose signage is in
/// Arabic has been given something they cannot show a taxi driver. Names of missions are also
/// genuinely different across languages rather than transliterated — an editor writes both.
/// </para>
/// <para>
/// The name is required and the rest is optional, because a partial translation of a directory entry
/// is useful in a way a partial translation of an official instruction is not: a reader who sees the
/// mission's name in their language and its address in the local one has lost nothing.
/// </para>
/// </summary>
public sealed class EmbassyLocalization
{
    private EmbassyLocalization(string locale, string name, string? description, string? address)
    {
        Locale = locale;
        Name = name;
        Description = description;
        Address = address;
    }

    private EmbassyLocalization() { }

    /// <summary>A normalized BCP-47 tag. Half of the row's identity.</summary>
    public string Locale { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Address { get; private set; }

    internal static EmbassyLocalization? TryCreate(
        string? locale, string? name, string? description, string? address)
    {
        var normalizedLocale = IntelligenceNormalization.Locale(locale);
        var trimmedName = IntelligenceNormalization.Text(name, IntelligenceLimits.MaxEmbassyName);

        return normalizedLocale is null || trimmedName is null
            ? null
            : new EmbassyLocalization(
                normalizedLocale,
                trimmedName,
                IntelligenceNormalization.Text(
                    description, IntelligenceLimits.MaxAuthorityDescription),
                IntelligenceNormalization.Text(address, IntelligenceLimits.MaxAddress));
    }
}
