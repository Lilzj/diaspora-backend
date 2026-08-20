namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// Whether somebody has confirmed that the authority really issued this.
/// <para>
/// <b>Distinct from <see cref="NoticeStatus"/>, and the two must not be merged.</b> Status is where
/// the notice is in an editorial workflow; this is what is known about its provenance. A published
/// notice whose source later turns out to be a forwarded screenshot becomes
/// <see cref="Rejected"/> here without moving in the workflow, and that combination — live, and
/// known to be unconfirmed — is precisely the state an editor needs to see to act on it.
/// </para>
/// </summary>
public enum NoticeVerificationState
{
    /// <summary>Nobody has checked. The default, and honest about it.</summary>
    Unverified = 0,

    /// <summary>A claim has been raised with Trust and is waiting on a reviewer.</summary>
    Pending = 1,

    /// <summary>Confirmed against the authority's own publication.</summary>
    Verified = 2,

    /// <summary>Checked and not confirmed. The source did not hold up.</summary>
    Rejected = 3
}
