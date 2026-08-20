namespace Diaspora.Modules.Location.Domain.Enums;

/// <summary>
/// How someone would cover a distance, for the commute-time estimate.
/// <para>
/// The member names are lower-cased on the wire to match the frontend's
/// <c>'walk' | 'drive' | 'transit'</c> union, which <c>DistanceBadge</c> already branches on.
/// Recorded in the frontend contract report's enum inventory.
/// </para>
/// </summary>
public enum CommuteMode
{
    Walk = 0,
    Drive = 1,
    Transit = 2
}
