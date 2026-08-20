namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// How much weight a claimed credential carries.
/// <para>
/// A certification starts <see cref="Claimed"/> — the provider typed it in and nobody checked.
/// That is not nothing (it tells a member what to ask about) and it is not proof, and the
/// distinction has to survive to the client, because a "Gas Safe registered" badge that a
/// provider awarded themselves is the kind of thing people get hurt by.
/// </para>
/// <para>
/// <see cref="Verified"/> is set only from a Trust outcome, never by this module. The
/// separation is the same one <see cref="ProviderVerificationState"/> draws.
/// </para>
/// </summary>
public enum CertificationStatus
{
    /// <summary>Self-declared, unchecked. Rendered without a verification mark.</summary>
    Claimed = 1,

    /// <summary>Evidence submitted to Trust, awaiting an outcome.</summary>
    Submitted = 2,

    /// <summary>Trust checked the evidence and accepted it.</summary>
    Verified = 3,

    /// <summary>Trust checked the evidence and refused it.</summary>
    Rejected = 4,

    /// <summary>Was verified; the credential's own expiry has passed.</summary>
    Expired = 5,

    /// <summary>Withdrawn by the issuing body or by staff.</summary>
    Revoked = 6
}
