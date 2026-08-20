namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// A member's whole residence history, plus the summary lines a profile header renders.
/// <para>
/// The counts are computed once here rather than left to the client, because three screens
/// render them — the trust centre, the profile, and a contributor row — and three
/// implementations of "how many countries has this person verified" would eventually be two
/// different numbers.
/// </para>
/// </summary>
public sealed record CountryExperienceTimelineResponse
{
    /// <summary>Most recent arrival first.</summary>
    public required IReadOnlyList<CountryExperienceResponse> Experiences { get; init; }

    /// <summary>Where the member lives now, when any residence is still open.</summary>
    public CountryExperienceResponse? CurrentResidence { get; init; }

    public required int CountryCount { get; init; }

    public required int VerifiedCountryCount { get; init; }

    /// <summary>Total years across every residence, overlaps excluded by construction.</summary>
    public required int TotalYearsAbroad { get; init; }
}
