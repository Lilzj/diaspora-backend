namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Writes or revises a neighbourhood profile.
/// <para>
/// <b>It names a B4 area rather than creating a neighbourhood.</b> The place already exists in
/// Location and this describes it. An area identifier that is not at neighbourhood level is
/// refused, because a profile hanging off a city would claim to describe somewhere far larger than
/// anybody wrote about.
/// </para>
/// <para>
/// <b>Writing is not approving.</b> A change lands unapproved and the profile says so until
/// somebody with the approval permission signs it off — which is what makes these summaries safe
/// to let a wide set of contributors edit.
/// </para>
/// </summary>
public sealed record UpsertNeighborhoodProfileRequest
{
    /// <summary>B4's administrative area at neighbourhood level.</summary>
    public required Guid AreaId { get; init; }

    public string? Description { get; init; }

    public string? Overview { get; init; }

    public string? SafetySummary { get; init; }

    public string? TransportSummary { get; init; }

    public string? CostOfLivingSummary { get; init; }

    /// <summary>Who lives there. The paragraph a diaspora member reads first.</summary>
    public string? CommunitySummary { get; init; }

    public string? ClimateNotes { get; init; }

    // ── Liveability bands. Unknown, VeryLow, Low, Moderate, High, VeryHigh. ─────────────────

    public string? Walkability { get; init; }

    public string? FamilyFriendliness { get; init; }

    public string? StudentFriendliness { get; init; }

    public string? Nightlife { get; init; }

    /// <summary>High means loud, not good. The one band whose direction is inverted.</summary>
    public string? NoiseLevel { get; init; }

    public string? Accessibility { get; init; }

    public string? ParkingAvailability { get; init; }

    /// <summary>High means expensive.</summary>
    public string? CostOfLiving { get; init; }

    /// <summary>Unknown, None, MobileOnly, Basic, Standard, Fast or Gigabit.</summary>
    public string? InternetAvailability { get; init; }

    public IReadOnlyList<NeighborhoodLanguageRequest>? PopularLanguages { get; init; }

    public int? BaseRevision { get; init; }
}
