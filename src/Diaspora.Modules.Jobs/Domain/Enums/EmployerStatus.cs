namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// An employer's standing on the platform.
/// <para>
/// Distinct from its trust mark. A verified employer can still be suspended for how it behaves,
/// and an unverified one is perfectly entitled to post — verification is a badge, this is
/// permission to operate. Conflating them would mean either that misconduct costs a badge
/// Trust owns, or that a new employer cannot hire until Trust gets round to them.
/// </para>
/// </summary>
public enum EmployerStatus
{
    /// <summary>Registered and able to post.</summary>
    Active = 0,

    /// <summary>Barred from posting or recruiting by moderation. Reversible.</summary>
    Suspended = 1,

    /// <summary>Closed by its owner. Existing postings are archived.</summary>
    Closed = 2
}
