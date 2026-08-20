using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// The comparable half of a neighbourhood profile: eight bands a member can line up side by side.
/// <para>
/// <b>This exists because comparing places is the thing the narrative cannot do.</b> Two
/// paragraphs about two neighbourhoods are unreadable next to each other; two rows of bands are
/// exactly what somebody choosing between Peckham and Brixton wants. Every field is the same
/// five-band <see cref="NeighborhoodRating"/> so a comparison table needs no per-field logic.
/// </para>
/// <para>
/// <b><see cref="NeighborhoodRating.Unknown"/> is the default everywhere and is not a low
/// score.</b> A neighbourhood nobody has assessed must not sort below one somebody rated badly,
/// and a client rendering these must show a gap rather than an empty bar. <see cref="Assessed"/>
/// is what tells it which it is looking at.
/// </para>
/// <para>
/// <b>Internet is not a band</b> — see <see cref="InternetAvailability"/> for why the question
/// people ask is not "is it good" but "can I work from here".
/// </para>
/// </summary>
public sealed class NeighborhoodLivingIndex : ValueObject
{
    private NeighborhoodLivingIndex(
        NeighborhoodRating walkability,
        NeighborhoodRating familyFriendliness,
        NeighborhoodRating studentFriendliness,
        NeighborhoodRating nightlife,
        NeighborhoodRating noiseLevel,
        NeighborhoodRating accessibility,
        NeighborhoodRating parkingAvailability,
        NeighborhoodRating costOfLiving,
        InternetAvailability internetAvailability)
    {
        Walkability = walkability;
        FamilyFriendliness = familyFriendliness;
        StudentFriendliness = studentFriendliness;
        Nightlife = nightlife;
        NoiseLevel = noiseLevel;
        Accessibility = accessibility;
        ParkingAvailability = parkingAvailability;
        CostOfLiving = costOfLiving;
        InternetAvailability = internetAvailability;
    }

    private NeighborhoodLivingIndex() { }

    public NeighborhoodRating Walkability { get; private set; }

    public NeighborhoodRating FamilyFriendliness { get; private set; }

    public NeighborhoodRating StudentFriendliness { get; private set; }

    public NeighborhoodRating Nightlife { get; private set; }

    /// <summary>
    /// How loud it is. <b>High means loud, not good</b> — the one band whose direction is
    /// inverted, which is why a client must label it rather than colour it like the others.
    /// </summary>
    public NeighborhoodRating NoiseLevel { get; private set; }

    /// <summary>Step-free access, dropped kerbs, lifts. Rated, because it decides where somebody can live.</summary>
    public NeighborhoodRating Accessibility { get; private set; }

    public NeighborhoodRating ParkingAvailability { get; private set; }

    /// <summary>
    /// How expensive it is to live there. <b>High means expensive</b>, matching
    /// <see cref="NoiseLevel"/> in being a band where more is not better.
    /// </summary>
    public NeighborhoodRating CostOfLiving { get; private set; }

    public InternetAvailability InternetAvailability { get; private set; }

    /// <summary>Whether anybody has assessed any of it. What a client checks before rendering bars.</summary>
    public bool Assessed =>
        Walkability != NeighborhoodRating.Unknown
        || FamilyFriendliness != NeighborhoodRating.Unknown
        || StudentFriendliness != NeighborhoodRating.Unknown
        || Nightlife != NeighborhoodRating.Unknown
        || NoiseLevel != NeighborhoodRating.Unknown
        || Accessibility != NeighborhoodRating.Unknown
        || ParkingAvailability != NeighborhoodRating.Unknown
        || CostOfLiving != NeighborhoodRating.Unknown
        || InternetAvailability != InternetAvailability.Unknown;

    /// <summary>How many of the nine indexes have been assessed. Drives the completeness figure.</summary>
    public int AssessedCount =>
        new[]
        {
            Walkability, FamilyFriendliness, StudentFriendliness, Nightlife,
            NoiseLevel, Accessibility, ParkingAvailability, CostOfLiving
        }.Count(rating => rating != NeighborhoodRating.Unknown)
        + (InternetAvailability == InternetAvailability.Unknown ? 0 : 1);

    /// <summary>
    /// Nothing assessed. A fresh instance per access: this is an owned type, and a shared static
    /// one makes EF write nulls into a second aggregate holding it.
    /// </summary>
    public static NeighborhoodLivingIndex Unassessed =>
        new(NeighborhoodRating.Unknown,
            NeighborhoodRating.Unknown,
            NeighborhoodRating.Unknown,
            NeighborhoodRating.Unknown,
            NeighborhoodRating.Unknown,
            NeighborhoodRating.Unknown,
            NeighborhoodRating.Unknown,
            NeighborhoodRating.Unknown,
            InternetAvailability.Unknown);

    /// <summary>
    /// Records an assessment.
    /// <para>
    /// Total rather than validating: every parameter is an enum the compiler has already
    /// constrained, so there is no invalid input to refuse and no <c>Result</c> to unwrap at
    /// twenty call sites.
    /// </para>
    /// </summary>
    public static NeighborhoodLivingIndex Create(
        NeighborhoodRating walkability = NeighborhoodRating.Unknown,
        NeighborhoodRating familyFriendliness = NeighborhoodRating.Unknown,
        NeighborhoodRating studentFriendliness = NeighborhoodRating.Unknown,
        NeighborhoodRating nightlife = NeighborhoodRating.Unknown,
        NeighborhoodRating noiseLevel = NeighborhoodRating.Unknown,
        NeighborhoodRating accessibility = NeighborhoodRating.Unknown,
        NeighborhoodRating parkingAvailability = NeighborhoodRating.Unknown,
        NeighborhoodRating costOfLiving = NeighborhoodRating.Unknown,
        InternetAvailability internetAvailability = InternetAvailability.Unknown) =>
        new(walkability,
            familyFriendliness,
            studentFriendliness,
            nightlife,
            noiseLevel,
            accessibility,
            parkingAvailability,
            costOfLiving,
            internetAvailability);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Walkability;
        yield return FamilyFriendliness;
        yield return StudentFriendliness;
        yield return Nightlife;
        yield return NoiseLevel;
        yield return Accessibility;
        yield return ParkingAvailability;
        yield return CostOfLiving;
        yield return InternetAvailability;
    }
}
