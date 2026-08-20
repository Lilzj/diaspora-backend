namespace Diaspora.Modules.Location.Domain.Enums;

/// <summary>
/// What the member's device has said about location access.
/// <para>
/// Recorded server-side so a second device knows the answer before it asks, and so the
/// "GPS access disabled, using manual fallback" banner can render on first paint rather than
/// after a round trip to the OS.
/// </para>
/// <para>
/// Serialised lower-case to match the frontend's <c>PermissionStatus</c> union exactly. That
/// deviates from the platform's PascalCase enum convention and is kept deliberately —
/// intentional deviation #10 in the frontend contract report.
/// </para>
/// </summary>
public enum LocationPermissionStatus
{
    Undetermined = 0,
    Granted = 1,
    Denied = 2
}
