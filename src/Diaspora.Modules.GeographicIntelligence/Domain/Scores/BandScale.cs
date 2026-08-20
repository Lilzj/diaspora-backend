using System.Globalization;

namespace Diaspora.Modules.GeographicIntelligence.Domain.Scores;

/// <summary>
/// Turns B11's five-band assessment into the 0–100 figure the client's score type requires, and
/// says exactly what it did.
/// <para>
/// <b>This is an encoding, not an estimate, and the distinction is the whole of §54.</b> B11
/// deliberately records liveability as five ordinal bands rather than a number, and its own note
/// says why: "publishing walkability 73 from a human read would be a precision the platform does
/// not have." That reasoning is correct and it is preserved here — the band remains the datum, it
/// travels beside the number on every response, and the number is the midpoint of the band's fifth
/// of the range. Nothing is interpolated, no two neighbourhoods with the same band get different
/// numbers, and a client that renders the band instead of the number loses nothing.
/// </para>
/// <para>
/// <b>Why produce a number at all.</b> The client's <c>ScoreDimension.score</c> is a required
/// number and its comparison surface lines three neighbourhoods up against each other. Refusing to
/// encode would mean either changing a shipped frontend contract or leaving the dimension out —
/// and leaving out a band a moderator actually assessed would be withholding real data.
/// </para>
/// </summary>
public static class BandScale
{
    /// <summary>
    /// The midpoint of each fifth of 0–100.
    /// <para>
    /// Five bands over a hundred points is twenty points each; the midpoints are the centres of
    /// those buckets. A client charting these gets bars at 10, 30, 50, 70 and 90 and no false
    /// precision between them.
    /// </para>
    /// </summary>
    public static int ToScore(int band) => band switch
    {
        1 => 10,
        2 => 30,
        3 => 50,
        4 => 70,
        5 => 90,
        _ => 0
    };

    /// <summary>
    /// The band a name refers to, or null when nobody has assessed it.
    /// <para>
    /// Parses the names B11 publishes on its integration contract — the summary carries band
    /// <em>names</em> rather than an enum precisely so a consumer needs no Housing type — so this
    /// is the one place a spelling change on that contract would surface, rather than the fourth
    /// dimension quietly reading zero.
    /// </para>
    /// </summary>
    public static int? ParseBand(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "verylow" or "very-low" or "very low" => 1,
        "low" => 2,
        "moderate" or "medium" => 3,
        "high" => 4,
        "veryhigh" or "very-high" or "very high" => 5,
        _ => null
    };

    /// <summary>
    /// The sentence published beside an encoded band.
    /// <para>
    /// Not decoration: §26 and §30 both require a served figure to say how it was produced, and a
    /// number whose methodology is "a moderator picked one of five bands and we placed it at the
    /// centre of its fifth" is a very different claim from one whose methodology is a survey.
    /// Saying so is what makes publishing it defensible.
    /// </para>
    /// </summary>
    public static string Methodology(string bandName, string assessedBy) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Assessed by {0} as one of five ordinal bands and published as '{1}'. The 0–100 figure "
            + "is the midpoint of that band's fifth of the range and carries no precision beyond "
            + "the band itself.",
            assessedBy,
            bandName);
}
