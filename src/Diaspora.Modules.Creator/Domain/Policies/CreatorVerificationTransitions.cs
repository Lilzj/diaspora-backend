using Diaspora.Modules.Creator.Domain.Enums;

namespace Diaspora.Modules.Creator.Domain.Policies;

/// <summary>
/// The creator-programme verification state machine, declared once.
/// <para>
/// <b>The register draws it and this is that drawing, transcribed.</b>
/// </para>
/// <code>
/// NotEligible → Eligible → Draft → Submitted → UnderReview
///                                       ↓            ↓
///                        MoreInformationRequired ← ──┤
///                                       ↓            ↓
///                                   (resubmit)   Approved → Expired
///                                                    ↓
///                                               Rejected → AppealPending → Approved | Rejected
///                              (any) → Suspended
/// </code>
/// <para>
/// <b>A table rather than a switch inside the aggregate.</b> An illegal transition is refused in
/// one place, so the answer cannot differ between the submit handler, the review handler and the
/// sweep that expires an approval. §7 of the phase brief: "Illegal transitions must be rejected
/// server-side."
/// </para>
/// <para>
/// <b><see cref="CreatorVerificationState.Suspended"/> is reachable from everywhere and is the
/// only state that is.</b> The platform withdrawing somebody from the programme is not a step in
/// their application; it happens to it.
/// </para>
/// </summary>
public static class CreatorVerificationTransitions
{
    private static readonly Dictionary<CreatorVerificationState, CreatorVerificationState[]> Allowed =
        new()
        {
            [CreatorVerificationState.NotEligible] = [CreatorVerificationState.Eligible],

            [CreatorVerificationState.Eligible] = [CreatorVerificationState.Draft],

            [CreatorVerificationState.Draft] = [CreatorVerificationState.Submitted],

            [CreatorVerificationState.Submitted] =
            [
                CreatorVerificationState.UnderReview,

                // A reviewer can ask for more without picking the application up first. The register
                // draws the arrow from Submitted as well as from UnderReview.
                CreatorVerificationState.MoreInformationRequired
            ],

            [CreatorVerificationState.UnderReview] =
            [
                CreatorVerificationState.MoreInformationRequired,
                CreatorVerificationState.Approved,
                CreatorVerificationState.Rejected
            ],

            // The member edits and hands it back. Draft is the resubmission path the register draws.
            [CreatorVerificationState.MoreInformationRequired] =
            [
                CreatorVerificationState.Draft,
                CreatorVerificationState.Submitted
            ],

            [CreatorVerificationState.Approved] = [CreatorVerificationState.Expired],

            [CreatorVerificationState.Rejected] = [CreatorVerificationState.AppealPending],

            [CreatorVerificationState.AppealPending] =
            [
                CreatorVerificationState.Approved,
                CreatorVerificationState.Rejected
            ],

            // An approval that lapsed is a fresh start rather than a dead end.
            [CreatorVerificationState.Expired] = [CreatorVerificationState.Draft],

            // Reinstatement returns the application to the queue rather than to its decision.
            [CreatorVerificationState.Suspended] = [CreatorVerificationState.Eligible]
        };

    /// <summary>Whether the machine permits this move.</summary>
    public static bool IsAllowed(CreatorVerificationState from, CreatorVerificationState to)
    {
        // Suspension is the platform acting on the application rather than the application moving.
        if (to == CreatorVerificationState.Suspended)
        {
            return from != CreatorVerificationState.Suspended;
        }

        return Allowed.TryGetValue(from, out var targets)
            && Array.IndexOf(targets, to) >= 0;
    }

    /// <summary>States a reviewer is looking at rather than the member.</summary>
    public static bool IsWithReviewer(CreatorVerificationState state) =>
        state is CreatorVerificationState.Submitted
            or CreatorVerificationState.UnderReview
            or CreatorVerificationState.AppealPending;

    /// <summary>States the member may still edit.</summary>
    public static bool IsEditable(CreatorVerificationState state) =>
        state is CreatorVerificationState.Draft
            or CreatorVerificationState.MoreInformationRequired;

    /// <summary>States that carry the verified-creator standing.</summary>
    public static bool ConfersVerifiedCreator(CreatorVerificationState state) =>
        state == CreatorVerificationState.Approved;
}

/// <summary>
/// The grant application state machine.
/// <para>
/// <b>The backend owns it, and §10 of the phase brief is explicit about what that means:</b> "The
/// frontend must never decide approval, rejection, eligibility, grant award or grant completion."
/// Every arrow below is a server decision, and four of them need a reviewer permission the
/// applicant does not hold.
/// </para>
/// </summary>
public static class GrantApplicationTransitions
{
    private static readonly Dictionary<GrantApplicationState, GrantApplicationState[]> Allowed =
        new()
        {
            [GrantApplicationState.Draft] =
            [
                GrantApplicationState.Submitted,
                GrantApplicationState.Withdrawn
            ],

            [GrantApplicationState.Submitted] =
            [
                GrantApplicationState.UnderReview,
                GrantApplicationState.MoreInformationRequested,
                GrantApplicationState.Approved,
                GrantApplicationState.Rejected,
                GrantApplicationState.Withdrawn
            ],

            [GrantApplicationState.UnderReview] =
            [
                GrantApplicationState.MoreInformationRequested,
                GrantApplicationState.Approved,
                GrantApplicationState.Rejected,
                GrantApplicationState.Withdrawn
            ],

            [GrantApplicationState.MoreInformationRequested] =
            [
                GrantApplicationState.Submitted,
                GrantApplicationState.Withdrawn
            ],

            // Approved becomes Active when the funding leg completes. B15 moves the money; this
            // module records that it did — see Application/Financial/GrantFundingBoundary.
            [GrantApplicationState.Approved] = [GrantApplicationState.Active],

            [GrantApplicationState.Active] = [GrantApplicationState.Completed],

            // Terminal.
            [GrantApplicationState.Rejected] = [],
            [GrantApplicationState.Withdrawn] = [],
            [GrantApplicationState.Completed] = []
        };

    public static bool IsAllowed(GrantApplicationState from, GrantApplicationState to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>States the applicant may still edit the answers of.</summary>
    public static bool IsEditable(GrantApplicationState state) =>
        state is GrantApplicationState.Draft or GrantApplicationState.MoreInformationRequested;

    /// <summary>States the applicant may withdraw from.</summary>
    public static bool CanWithdraw(GrantApplicationState state) =>
        state is GrantApplicationState.Draft
            or GrantApplicationState.Submitted
            or GrantApplicationState.UnderReview
            or GrantApplicationState.MoreInformationRequested;

    /// <summary>States that stop a second application to the same grant.</summary>
    public static bool IsLive(GrantApplicationState state) =>
        state is not (GrantApplicationState.Rejected
            or GrantApplicationState.Withdrawn
            or GrantApplicationState.Completed);
}
