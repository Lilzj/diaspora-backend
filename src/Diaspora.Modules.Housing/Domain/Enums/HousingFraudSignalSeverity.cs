namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// How strongly a fraud provider objects.
/// <para>
/// Only <see cref="Blocking"/> stops a publication, and only when the module is configured to
/// enforce signals. Everything else is a note on a moderator's queue - because a heuristic that
/// silently refuses a genuine landlord's listing costs the platform the landlord.
/// </para>
/// </summary>
public enum HousingFraudSignalSeverity
{
    Informational = 0,
    Suspicious = 1,
    Blocking = 2
}
