namespace Diaspora.Contracts.Authorization;

/// <summary>
/// The platform-wide coarse ladder that authorisation rules are written against.
/// <para>
/// Identity owns the detailed nine-state <c>AccessState</c> machine and projects it onto
/// these tiers at the boundary. Every other module gates on the tier, which is why a
/// Marketplace or Jobs module can express "verified members only" without referencing a
/// single Identity type — and why Identity can add a tenth state without breaking them.
/// </para>
/// <para>
/// The values are ordered and comparable. A capability declares the lowest tier that may
/// use it, and optionally the highest, so "available only until you have asked for
/// verification" is expressible without a special case.
/// </para>
/// </summary>
public enum AccessTier
{
    /// <summary>No authenticated subject. Nothing is granted at this tier.</summary>
    Anonymous = 0,

    /// <summary>Account exists but onboarding is incomplete. Identity only.</summary>
    Onboarding = 1,

    /// <summary>Onboarded. Can read the platform and manage their own account.</summary>
    Limited = 2,

    /// <summary>Verification has been requested and is with the Trust Engine.</summary>
    PendingVerification = 3,

    /// <summary>Verified. The contributor tier.</summary>
    Verified = 4
}
