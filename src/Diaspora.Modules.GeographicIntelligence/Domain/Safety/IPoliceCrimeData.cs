namespace Diaspora.Modules.GeographicIntelligence.Domain.Safety;

/// <summary>
/// Police-recorded street crime around a point, from an official open-data source.
/// <para>
/// <b>Counts, never a rating.</b> The user decided on 16 Sep 2026 that safety is published as sourced
/// incident counts with no score. This seam answers how many crimes the police recorded near a place
/// over whole calendar months, by category, and says which months — the only kind of figure the
/// source can back.
/// </para>
/// <para>
/// Today one source implements it: data.police.uk, which covers England, Wales and Northern Ireland
/// under the Open Government Licence. Nothing else in the world is covered, and a caller shows no
/// police figure rather than a zero for a place the source does not cover.
/// </para>
/// </summary>
public interface IPoliceCrimeData
{
    /// <summary>Whether the source publishes data for places in this country at all.</summary>
    bool Covers(string countryCode);

    /// <summary>
    /// Crimes recorded within the source's fixed radius of the point over its most recent months.
    /// </summary>
    /// <returns>
    /// The counts; or an answer that says why there are none — the source could not be read, or it
    /// holds nothing for this point.
    /// </returns>
    Task<PoliceCrimeAnswer> CountAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

/// <summary>What the police source said about a point.</summary>
public sealed record PoliceCrimeAnswer
{
    public PoliceCrimeCounts? Counts { get; init; }

    /// <summary>Why <see cref="Counts"/> is null: <see cref="PoliceCrimeUnavailable"/>.</summary>
    public string? UnavailableReason { get; init; }

    public static PoliceCrimeAnswer Of(PoliceCrimeCounts counts) => new() { Counts = counts };

    public static PoliceCrimeAnswer Unavailable(string reason) => new() { UnavailableReason = reason };
}

/// <summary>The reasons a police answer carries no counts.</summary>
public static class PoliceCrimeUnavailable
{
    /// <summary>This deployment does not read the source.</summary>
    public const string Disabled = "Disabled";

    /// <summary>The source did not answer, answered an error, or answered something unreadable.</summary>
    public const string SourceUnavailable = "SourceUnavailable";

    /// <summary>
    /// The source answered and holds no crime at all near the point for a whole year — which, for a
    /// populated place, means it does not cover the point (Scotland, for one), not that nothing happened.
    /// </summary>
    public const string NoCoverage = "NoCoverage";
}

/// <summary>Crimes the police recorded near a point, per category, over whole months.</summary>
public sealed record PoliceCrimeCounts
{
    /// <summary>The first month counted, as the source names it: <c>2025-07</c>.</summary>
    public required string FirstMonth { get; init; }

    /// <summary>The last month counted: <c>2026-06</c>.</summary>
    public required string LastMonth { get; init; }

    public required int Months { get; init; }

    /// <summary>Counts keyed by the source's category identifier, e.g. <c>anti-social-behaviour</c>.</summary>
    public required IReadOnlyDictionary<string, int> ByCategory { get; init; }

    /// <summary>The source's own name for each category identifier.</summary>
    public required IReadOnlyDictionary<string, string> CategoryNames { get; init; }

    public int Total => ByCategory.Values.Sum();
}
