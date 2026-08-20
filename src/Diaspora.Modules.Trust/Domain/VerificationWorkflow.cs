using System.Collections.Frozen;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// The legal transitions of a verification request, as data.
/// <para>
/// A table rather than a pile of <c>if</c>s in the aggregate, for the same reason Identity's
/// access-state machine is a table: the set of legal moves is the part reviewers and
/// auditors ask about, and it should be readable in one screen without tracing control flow.
/// A move that is not in this table cannot happen — <see cref="VerificationRequest"/> asks
/// here before every status change.
/// </para>
/// <para>
/// "Everything should be configurable" in the brief is served by
/// <see cref="IVerificationWorkflowPolicy"/>, which lets a module supply a narrower table
/// per verification type. This is the platform default and the widest legal set.
/// </para>
/// </summary>
public static class VerificationWorkflow
{
    private static readonly FrozenDictionary<VerificationStatus, FrozenSet<VerificationStatus>> Transitions =
        new Dictionary<VerificationStatus, VerificationStatus[]>
        {
            // A fresh request either enters the queue or is decided immediately by a rule.
            [VerificationStatus.Submitted] =
            [
                VerificationStatus.Pending,
                VerificationStatus.Approved,
                VerificationStatus.Rejected
            ],

            // Queued: a reviewer picks it up, a rule decides it, or it lapses unattended.
            [VerificationStatus.Pending] =
            [
                VerificationStatus.UnderReview,
                VerificationStatus.NeedMoreInfo,
                VerificationStatus.UnderInvestigation,
                VerificationStatus.Approved,
                VerificationStatus.PartiallyApproved,
                VerificationStatus.Rejected,
                VerificationStatus.Expired
            ],

            // Open on a reviewer's desk.
            [VerificationStatus.UnderReview] =
            [
                VerificationStatus.NeedMoreInfo,
                VerificationStatus.UnderInvestigation,
                VerificationStatus.Approved,
                VerificationStatus.PartiallyApproved,
                VerificationStatus.Rejected,
                VerificationStatus.Pending // released back to the queue
            ],

            // Waiting on the member. They answer (back to Pending) or they do not (Expired).
            [VerificationStatus.NeedMoreInfo] =
            [
                VerificationStatus.Pending,
                VerificationStatus.UnderReview,
                VerificationStatus.Rejected,
                VerificationStatus.Expired
            ],

            // Escalated. Only a decision or a return to review ends this.
            [VerificationStatus.UnderInvestigation] =
            [
                VerificationStatus.UnderReview,
                VerificationStatus.Approved,
                VerificationStatus.PartiallyApproved,
                VerificationStatus.Rejected
            ],

            // Granted. Evidence can lapse, trust can be withdrawn, or a re-check can be asked for.
            [VerificationStatus.Approved] =
            [
                VerificationStatus.Expired,
                VerificationStatus.Revoked,
                VerificationStatus.ReverificationRequired
            ],

            // Partially granted behaves like granted, and may be completed by a later review.
            [VerificationStatus.PartiallyApproved] =
            [
                VerificationStatus.Approved,
                VerificationStatus.Expired,
                VerificationStatus.Revoked,
                VerificationStatus.ReverificationRequired,
                VerificationStatus.Appealed
            ],

            // Refused. The member's only move is to appeal; staff may reopen on new evidence.
            [VerificationStatus.Rejected] =
            [
                VerificationStatus.Appealed,
                VerificationStatus.Pending
            ],

            // Lapsed. Re-submitting reopens the queue.
            [VerificationStatus.Expired] =
            [
                VerificationStatus.Pending
            ],

            // Withdrawn for cause. Appealable, like a rejection.
            [VerificationStatus.Revoked] =
            [
                VerificationStatus.Appealed,
                VerificationStatus.Pending
            ],

            // Under appeal. The appeal's outcome writes back here.
            [VerificationStatus.Appealed] =
            [
                VerificationStatus.Approved,
                VerificationStatus.PartiallyApproved,
                VerificationStatus.Rejected,
                VerificationStatus.UnderReview
            ],

            // Re-check demanded. The member resubmits, or loses the grant.
            [VerificationStatus.ReverificationRequired] =
            [
                VerificationStatus.Pending,
                VerificationStatus.Expired,
                VerificationStatus.Revoked
            ]
        }
        .ToFrozenDictionary(pair => pair.Key, pair => pair.Value.ToFrozenSet());

    /// <summary>Statuses from which no further move is possible without a new request.</summary>
    private static readonly FrozenSet<VerificationStatus> Grants =
        new[] { VerificationStatus.Approved, VerificationStatus.PartiallyApproved }.ToFrozenSet();

    private static readonly FrozenSet<VerificationStatus> Open =
    [
        VerificationStatus.Submitted,
        VerificationStatus.Pending,
        VerificationStatus.UnderReview,
        VerificationStatus.NeedMoreInfo,
        VerificationStatus.UnderInvestigation,
        VerificationStatus.Appealed
    ];

    /// <summary>Whether <paramref name="to"/> is reachable from <paramref name="from"/>.</summary>
    public static bool CanTransition(VerificationStatus from, VerificationStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>Every status reachable from <paramref name="from"/> in one move.</summary>
    public static IReadOnlyCollection<VerificationStatus> NextFrom(VerificationStatus from) =>
        Transitions.TryGetValue(from, out var allowed) ? allowed : [];

    /// <summary>Whether this status means the member currently holds the verification.</summary>
    public static bool IsGrant(VerificationStatus status) => Grants.Contains(status);

    /// <summary>Whether this status still needs someone to do something.</summary>
    public static bool IsOpen(VerificationStatus status) => Open.Contains(status);

    /// <summary>Whether a member may appeal from this status.</summary>
    public static bool IsAppealable(VerificationStatus status) =>
        status is VerificationStatus.Rejected or VerificationStatus.Revoked
            or VerificationStatus.PartiallyApproved;
}
