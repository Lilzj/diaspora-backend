namespace Diaspora.Modules.Location.Domain.Enums;

/// <summary>
/// The state of a residency claim as far as the Trust Engine has told this module.
/// <para>
/// This is a projection, not a decision. Every transition here is caused by an integration
/// event Trust published; Location never moves a residency into
/// <see cref="Verified"/> on its own reasoning, because deciding whether a claim is proven is
/// precisely the responsibility ADR-002 gives to Trust and takes away from everyone else.
/// </para>
/// </summary>
public enum ResidencyVerificationState
{
    /// <summary>Trust has said nothing about this country for this member.</summary>
    Unverified = 0,

    /// <summary>Trust accepted a submission and has not decided yet.</summary>
    UnderReview = 1,

    /// <summary>Trust approved it. The grant may still carry an expiry.</summary>
    Verified = 2,

    /// <summary>Trust rejected the submission.</summary>
    Rejected = 3,

    /// <summary>A previously granted verification lapsed on its own expiry.</summary>
    Expired = 4,

    /// <summary>A previously granted verification was withdrawn for cause.</summary>
    Revoked = 5
}
