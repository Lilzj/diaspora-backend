namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// The comparable half of a neighbourhood profile: bands a member can line up side by side.
/// <para>
/// Every band is Unknown, VeryLow, Low, Moderate, High or VeryHigh. <b>Unknown is not a low score</b>
/// - a neighbourhood nobody has assessed must render as a gap rather than an empty bar, and
/// <c>Assessed</c> is what tells a client which it is looking at.
/// </para>
/// </summary>
public sealed record NeighborhoodLivingIndexResponse
{
    public required string Walkability { get; init; }

    public required string FamilyFriendliness { get; init; }

    public required string StudentFriendliness { get; init; }

    public required string Nightlife { get; init; }

    /// <summary>High means loud, not good. The one band whose direction is inverted.</summary>
    public required string NoiseLevel { get; init; }

    public required string Accessibility { get; init; }

    public required string ParkingAvailability { get; init; }

    /// <summary>High means expensive.</summary>
    public required string CostOfLiving { get; init; }

    /// <summary>Unknown, None, MobileOnly, Basic, Standard, Fast or Gigabit.</summary>
    public required string InternetAvailability { get; init; }

    /// <summary>Whether anybody has assessed any of it.</summary>
    public required bool Assessed { get; init; }
}
