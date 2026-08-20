namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// A trust mark's state, as projected from Trust onto a posting or an employer.
/// <para>
/// <b>Nothing in this module may set this to <see cref="Verified"/> on its own authority.</b>
/// The value is a projection of a decision Trust made, applied only by a handler for a Trust
/// integration event. A module that could verify itself would make every badge on the platform
/// worthless — which is the line ADR-002 draws, and the reason B3.0.2's submission contract
/// returns a case identifier rather than a decision.
/// </para>
/// </summary>
public enum JobVerificationState
{
    /// <summary>Never submitted, or the outcome was cleared.</summary>
    None = 0,

    /// <summary>A case is open with Trust.</summary>
    Pending = 1,

    /// <summary>Trust approved it.</summary>
    Verified = 2,

    /// <summary>Trust withdrew a mark it had granted.</summary>
    Revoked = 3,

    /// <summary>Trust declined the claim.</summary>
    Rejected = 4
}
