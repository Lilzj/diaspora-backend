using System.ComponentModel.DataAnnotations;

namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// Bounds on the street-address search, whichever vendor answers it.
/// <para>
/// The per-member limits are an abuse fence rather than the budget. The budget is the vendor
/// adapter's monthly allowance, shared by every member. Without these, one member holding a key
/// down could spend all of it. They are per machine, like every <c>ISubjectRateLimiter</c> budget
/// today, which is loose but still far below the allowance.
/// </para>
/// </summary>
public sealed class LocationPlacesOptions
{
    public const string SectionName = "Location:Places";

    [Range(200, 15000)]
    public int TimeoutMilliseconds { get; set; } = 3000;

    /// <summary>Suggestion requests one member may make in <see cref="MemberWindowMinutes"/>.</summary>
    [Range(1, 10000)]
    public int AutocompletePerMember { get; set; } = 120;

    /// <summary>Picks one member may make in <see cref="MemberWindowMinutes"/>.</summary>
    [Range(1, 10000)]
    public int DetailsPerMember { get; set; } = 30;

    [Range(1, 1440)]
    public int MemberWindowMinutes { get; set; } = 60;
}
