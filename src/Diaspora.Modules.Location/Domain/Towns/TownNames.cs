using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Location.Domain.Towns;

/// <summary>
/// How a town's name is compared with what a member typed, without any AI.
/// <para>
/// Both sides are written the same way first: accents taken off, lower case, anything that is
/// not a letter or a digit turned into a space ("Mahébourg" and "mahebourg" are the same town,
/// so are "Grand-Baie" and "grand baie"). <c>scripts/load-world-towns.py</c> writes the stored
/// names by the same rule. Then a close spelling still counts: "grand bay" is one letter and one
/// more away from "grand baie", which is close enough; "granby" is not, and is a town in Canada.
/// </para>
/// </summary>
public static class TownNames
{
    /// <summary>A name this close or closer counts as the town (1 is the same name).</summary>
    public const double CloseEnough = 0.8;

    /// <summary>The town's own name and its plain spelling, and a few of its other spellings.</summary>
    public const int MaxNamesPerTown = 8;

    /// <summary>
    /// A city this big is known by many names abroad ("Londres", "Le Cap"), and GeoNames lists
    /// them about alphabetically, so it keeps more of them.
    /// </summary>
    public const long BigCityPopulation = 100_000;

    public const int MaxNamesPerBigCity = 32;

    public const int MaxNameLength = 60;

    /// <summary>
    /// The shortest typed name compared with the words inside a town's name: "la", "st" or "bay"
    /// would find a town in every country.
    /// </summary>
    public const int MinPartLength = 4;

    /// <summary>The longest <see cref="WorldTown.SearchNames"/> the column holds.</summary>
    public const int MaxSearchNamesLength = 1000;

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var lastWasSpace = true;

        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    public static int MaxNamesFor(long population) =>
        population >= BigCityPopulation ? MaxNamesPerBigCity : MaxNamesPerTown;

    /// <summary>
    /// An airport or postal code among a town's other spellings ("PLU", "MRU" for Port Louis):
    /// four capital letters or fewer. Nobody types one meaning the town, and it would match
    /// the start of unrelated words.
    /// </summary>
    public static bool IsCode(string? spelling)
    {
        var trimmed = spelling?.Trim() ?? string.Empty;

        return trimmed.Length is > 0 and <= 4 && trimmed.All(character => character is >= 'A' and <= 'Z');
    }

    /// <summary>
    /// A name worth keeping for search: written in plain letters a member can type here
    /// (other scripts are left out, the list's own name already covers the town), and short.
    /// </summary>
    public static bool IsSearchable(string normalized) =>
        normalized.Length is >= 2 and <= MaxNameLength
        && normalized.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or ' ');

    /// <summary>
    /// How close two names are, from 0 to 1, after <see cref="Normalize"/>: one less the edits
    /// between them over the longer one's length, and the better of comparing them with and
    /// without their spaces ("grandbaie" is "grand baie").
    /// </summary>
    public static double Closeness(string typed, string name)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(name);

        if (typed.Length == 0 || name.Length == 0)
        {
            return 0;
        }

        return Math.Max(
            Ratio(typed, name),
            Ratio(typed.Replace(" ", string.Empty, StringComparison.Ordinal), name.Replace(" ", string.Empty, StringComparison.Ordinal)));
    }

    /// <summary>
    /// How close what was typed is to a run of whole words inside a name, from 0 to 1 (5 Nov 2026):
    /// GeoNames files Rose Hill as "Beau Bassin-Rose Hill" and Vacoas as "Vacoas-Phoenix", and a
    /// member types the part they call it. The whole name is <see cref="Closeness"/>'s to judge, and
    /// a typed name shorter than <see cref="MinPartLength"/> is never compared with parts.
    /// </summary>
    public static double PartCloseness(string typed, string name)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(name);

        if (typed.Replace(" ", string.Empty, StringComparison.Ordinal).Length < MinPartLength)
        {
            return 0;
        }

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var best = 0d;

        for (var start = 0; start < words.Length; start++)
        {
            for (var length = 1; start + length <= words.Length; length++)
            {
                if (length == words.Length)
                {
                    continue;
                }

                var part = string.Join(' ', words, start, length);

                if (part.Replace(" ", string.Empty, StringComparison.Ordinal).Length >= MinPartLength)
                {
                    best = Math.Max(best, Closeness(typed, part));
                }
            }
        }

        return best;
    }

    private static double Ratio(string left, string right)
    {
        if (left == right)
        {
            return 1;
        }

        var longer = Math.Max(left.Length, right.Length);

        return longer == 0 ? 1 : 1 - (double)Distance(left, right) / longer;
    }

    /// <summary>Levenshtein: the fewest letters added, removed or changed to turn one into the other.</summary>
    private static int Distance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
