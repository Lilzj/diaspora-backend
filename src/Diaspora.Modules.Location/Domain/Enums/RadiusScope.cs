namespace Diaspora.Modules.Location.Domain.Enums;

/// <summary>
/// How far "nearby" reaches.
/// <para>
/// The frontend's <c>RadiusSlider</c> encodes the two non-metric options as negative
/// distances — <c>-1</c> for city-wide and <c>-2</c> for country-wide. A negative radius is
/// not a distance, and feeding one into a spatial index produces an empty result rather than
/// an error, which is the worst possible failure mode. This enum is the honest form of the
/// same choice; the API edge accepts the sentinels and translates them, so no client change
/// is forced. Recorded as intentional deviation #4 in the frontend contract report.
/// </para>
/// </summary>
public enum RadiusScope
{
    /// <summary>Within a given distance of a point. The radius is meaningful.</summary>
    Radius = 0,

    /// <summary>Everything in the containing city, however far. The radius is ignored.</summary>
    City = 1,

    /// <summary>Everything in the containing country. The radius is ignored.</summary>
    Country = 2
}
