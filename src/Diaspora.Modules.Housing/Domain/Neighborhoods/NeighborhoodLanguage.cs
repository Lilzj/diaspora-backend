using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Policies;

namespace Diaspora.Modules.Housing.Domain.Neighborhoods;

/// <summary>
/// A language you will hear in a neighbourhood, and how much of it.
/// <para>
/// <b>The single most relevant line on a neighbourhood profile for the people this platform
/// exists for.</b> Somebody moving to a country where they do not speak the majority language is
/// choosing, among other things, a place where they can be understood at the chemist. No
/// liveability band expresses that and no prose paragraph is filterable on it.
/// </para>
/// <para>
/// A prominence band rather than a percentage, for the reason <see cref="NeighborhoodRating"/>
/// gives: this is an editorial or community judgement, and it is real to within about a fifth of
/// the range. Publishing "31% Yoruba" from a human read would be a precision nobody measured.
/// </para>
/// </summary>
public sealed class NeighborhoodLanguage
{
    private NeighborhoodLanguage(string name, NeighborhoodRating prominence)
    {
        Name = name;
        Prominence = prominence;
    }

    private NeighborhoodLanguage() { }

    /// <summary>
    /// The language's name as members would say it. Free text rather than an ISO code: the answer
    /// is often "Krio" or "Patois", which language-code registries handle badly and members use
    /// daily.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>How widely it is heard. See the class summary for why this is a band.</summary>
    public NeighborhoodRating Prominence { get; private set; }

    public static NeighborhoodLanguage Create(string? name, NeighborhoodRating prominence)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length > HousingLimits.MaxLanguageName)
        {
            trimmed = trimmed[..HousingLimits.MaxLanguageName];
        }

        return new NeighborhoodLanguage(trimmed, prominence);
    }
}
