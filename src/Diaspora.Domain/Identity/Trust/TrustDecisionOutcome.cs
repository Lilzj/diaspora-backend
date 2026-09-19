namespace Diaspora.Domain.Identity.Trust;

/// <summary>
/// The verdict the Trust &amp; Verification Engine (Phase B3) hands back to Identity.
/// <para>
/// Identity never computes one of these. It only records the consequence on the
/// access ladder, which is what keeps trust policy free to evolve without touching
/// the authentication subsystem.
/// </para>
/// </summary>
public enum TrustDecisionOutcome
{
    /// <summary>Review has begun; no verdict yet.</summary>
    UnderReview = 0,

    /// <summary>
    /// Submitted and waiting for a reviewer to pick it up.
    /// <para>
    /// <b>The rung that was missing.</b> The access ladder is
    /// <c>LimitedAccess -> VerificationPending -> VerificationUnderReview -> Verified</c>, and
    /// <see cref="UnderReview"/> targets the third rung. Publishing it on submission asked a
    /// member at <c>LimitedAccess</c> to skip one, which the transition table refuses — so a
    /// submission could never move anybody, whatever the transport did.
    /// </para>
    /// </summary>
    Submitted = 4,

    /// <summary>Verification granted.</summary>
    Granted = 1,

    /// <summary>Verification refused; the member falls back to limited access.</summary>
    Rejected = 2,

    /// <summary>Previously granted verification withdrawn.</summary>
    Revoked = 3
}
