using System.Globalization;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Turns an ISO country code into a display name and a flag emoji.
/// <para>
/// The frontend's <c>CountryReputationScore</c> and <c>CountryExperienceInterval</c> both
/// carry <c>countryName</c> and <c>flag</c> beside the code, and its screens render them
/// directly. The server therefore has to supply them — and does so by derivation rather than
/// by storing a name and a flag on every country standing row, which would be three copies
/// of the same fact and two of them stale.
/// </para>
/// </summary>
public static class CountryCatalog
{
    /// <summary>
    /// The regional-indicator block. A flag emoji is just the two letters of the country code
    /// shifted into it, which is why this needs no lookup table of 250 emoji.
    /// </summary>
    private const int RegionalIndicatorBase = 0x1F1E6;

    private const int LetterA = 'A';

    /// <summary>
    /// The flag emoji for a country code, or a globe when the code is not two letters.
    /// </summary>
    public static string FlagFor(string? countryCode)
    {
        if (!IsWellFormed(countryCode))
        {
            return "🌍";
        }

        var upper = countryCode!.Trim().ToUpperInvariant();

        return string.Concat(
            char.ConvertFromUtf32(RegionalIndicatorBase + (upper[0] - LetterA)),
            char.ConvertFromUtf32(RegionalIndicatorBase + (upper[1] - LetterA)));
    }

    /// <summary>
    /// The English display name for a country code.
    /// <para>
    /// Uses <see cref="RegionInfo"/> rather than a hand-maintained table: ICU already ships
    /// with the platform and stays current, and a table of country names in this file would
    /// be wrong within a year and wrong in a way nobody notices.
    /// </para>
    /// <para>
    /// Falls back to the code itself for anything ICU does not recognise, which keeps a
    /// screen rendering "XK" rather than rendering nothing.
    /// </para>
    /// </summary>
    public static string NameFor(string? countryCode)
    {
        if (!IsWellFormed(countryCode))
        {
            return countryCode?.Trim() ?? string.Empty;
        }

        var upper = countryCode!.Trim().ToUpperInvariant();

        try
        {
            return new RegionInfo(upper).EnglishName;
        }
        catch (ArgumentException)
        {
            return upper;
        }
    }

    private static bool IsWellFormed(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        var trimmed = countryCode.Trim();

        return trimmed.Length == 2
            && char.IsAsciiLetter(trimmed[0])
            && char.IsAsciiLetter(trimmed[1]);
    }
}
