namespace Diaspora.Application.Abstractions;

/// <summary>
/// The named things a step-up grant can authorise.
/// <para>
/// <b>A scope is a blast radius, not a label.</b> A member who re-proved themselves to add a payout
/// account has not authorised a payout, and the two are separated here so that one proof cannot do
/// both. Scopes are deliberately few and coarse: one per group of operations a member would
/// reasonably perform in a single sitting, because a scope per command would mean a code per click
/// and a member who stops reading them.
/// </para>
/// <para>
/// These strings are contract. They travel to the client in the <c>stepUpScope</c> extension of the
/// refusal, and the client sends one back when it asks for a code — so renaming one breaks a flow
/// that is already deployed.
/// </para>
/// </summary>
public static class StepUpScopes
{
    /// <summary>
    /// Changing where money can be sent: registering a destination, attaching an identity to one,
    /// verifying one, or asking the provider to re-check one.
    /// <para>
    /// Refreshing is in here, and that is the point BSEC-007 made that is easiest to miss. Every
    /// production corridor sets <c>RequiresVerifiedPayee</c>, which looks like it bounds a new
    /// destination — but the member can drive that verification themselves against the provider,
    /// so the guard is only as strong as the weakest route into it.
    /// </para>
    /// </summary>
    public const string PayoutDestination = "payout-destination";

    /// <summary>Taking money off the platform.</summary>
    public const string Withdrawal = "withdrawal";

    /// <summary>Every scope the platform defines. The gate refuses an attribute naming anything else.</summary>
    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { PayoutDestination, Withdrawal };
}

/// <summary>
/// Marks a request that needs a fresh proof of identity, not merely a valid token.
/// <para>
/// <b>What it buys.</b> A bearer token proves that somebody signed in once, within the token's
/// lifetime, from somewhere. It does not prove that the person holding it right now is the member.
/// For most of the platform that distinction does not pay for itself; for the handful of operations
/// that move money or change where money goes, it is the difference between a stolen credential and
/// a lost balance.
/// </para>
/// <para>
/// <b>Why a window and not a single use.</b> A grant admits every request in its scope until it
/// expires. Consuming it on first use would read as stronger and would in fact be weaker: a
/// withdrawal is idempotent by key and a client that retries a timed-out request would be refused
/// on the retry, so the member would step up again and the platform would have taught them that
/// approving twice is normal. The window is short, the scope is narrow, and the grant is bound to
/// the one session that earned it.
/// </para>
/// <para>
/// <b>No amount threshold, which departs from the finding's wording.</b> BSEC-007 recommends step-up
/// on withdrawals "above a configurable amount". A per-request threshold is an instruction to slice:
/// a balance is drained in sub-threshold requests by an attacker who reads the same configuration
/// the member does. Because the grant lasts a window, gating every withdrawal costs a member one
/// code per sitting rather than one per request, so the concession buys nothing and gives up the
/// control. Recorded here rather than in a commit message because the next person to read this will
/// wonder where the threshold went.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresStepUpAttribute : Attribute
{
    public RequiresStepUpAttribute(string scope)
    {
        Scope = scope;
    }

    /// <summary>One of <see cref="StepUpScopes"/>.</summary>
    public string Scope { get; }
}
