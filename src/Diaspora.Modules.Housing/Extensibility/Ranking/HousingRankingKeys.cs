namespace Diaspora.Modules.Housing.Extensibility.Ranking;

/// <summary>
/// The ranking keys this module ships.
/// <para>
/// Constants rather than an enum, because the set is open: a contributor's strategy has a key this
/// class has never heard of, and an enum would make adding one an edit to Housing.
/// </para>
/// </summary>
public static class HousingRankingKeys
{
    /// <summary>Newest first. The default, and what a browse screen shows.</summary>
    public const string Recency = "recency";

    /// <summary>Verified landlords first, then newest.</summary>
    public const string Trusted = "trusted";

    /// <summary>
    /// Recency, trust and responsiveness together. What a member actually wants: a recent listing
    /// from a verified landlord who answers viewing requests.
    /// </summary>
    public const string Balanced = "balanced";

    /// <summary>Ready to move into soonest. What somebody with a flight booked sorts by.</summary>
    public const string Soonest = "soonest";
}
