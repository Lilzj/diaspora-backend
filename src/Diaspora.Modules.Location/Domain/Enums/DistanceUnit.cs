namespace Diaspora.Modules.Location.Domain.Enums;

/// <summary>
/// The unit a distance is expressed in.
/// <para>
/// The platform computes and stores in kilometres exclusively; this exists so a member in a
/// country that reads miles is shown miles. Conversion happens at the edge, never in the
/// domain — a single stored unit is what makes distances comparable across every module.
/// </para>
/// </summary>
public enum DistanceUnit
{
    Kilometers = 0,
    Miles = 1
}
