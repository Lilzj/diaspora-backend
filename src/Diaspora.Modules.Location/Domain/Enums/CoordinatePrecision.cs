namespace Diaspora.Modules.Location.Domain.Enums;

/// <summary>
/// How precisely a member's coordinates may be disclosed.
/// <para>
/// Identity owns the setting — it is <c>UserPrivacySettings.locationPrecision</c> on the
/// frontend — and Location owns the enforcement, because Location is the only module that
/// ever emits a coordinate. <see cref="Approximate"/> rounds to roughly a kilometre before
/// the value leaves the process, which is coarse enough that a home address cannot be
/// recovered from a sequence of readings.
/// </para>
/// </summary>
public enum CoordinatePrecision
{
    /// <summary>The coordinate as recorded.</summary>
    Exact = 0,

    /// <summary>Rounded to two decimal places — about 1.1 km at the equator.</summary>
    Approximate = 1
}
