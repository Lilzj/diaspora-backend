namespace Diaspora.Domain.Identity.Security;

/// <summary>
/// How much attention a security event deserves. Drives alerting thresholds
/// downstream; Identity only classifies.
/// </summary>
public enum SecurityEventSeverity
{
    /// <summary>Routine and expected.</summary>
    Informational = 0,

    /// <summary>Unusual but explainable on its own.</summary>
    Notice = 1,

    /// <summary>Suspicious in aggregate — repeated failures, lockouts.</summary>
    Warning = 2,

    /// <summary>Evidence of compromise. Token replay lives here.</summary>
    Critical = 3
}
