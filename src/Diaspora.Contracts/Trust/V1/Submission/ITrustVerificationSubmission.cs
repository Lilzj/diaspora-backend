using Diaspora.SharedKernel.Results;

namespace Diaspora.Contracts.Trust.V1.Submission;

/// <summary>
/// The in-process way into the Trust verification workflow.
/// <para>
/// <b>This is the half of the Trust lifecycle that was missing.</b> Trust has always published its
/// outcomes — approved, rejected, expired, revoked — so any module could react to a decision. It
/// published no way to <em>ask</em> for one. A vertical that wanted its aggregate verified had
/// three bad options: call Trust's HTTP endpoint from inside the same process, reference Trust's
/// handlers, or reimplement the workflow. B7 took a fourth and returned a stub. This interface is
/// the supported answer, and it closes the loop: submission → moderation → outcome, all through
/// published contracts.
/// </para>
/// <para>
/// <b>What a caller may do:</b> ask. That is the whole surface. Approval, rejection, revocation,
/// expiry, badge assignment, trust scoring and outcome publication are Trust's alone and are not
/// reachable from here — by design, not by omission. A module that could verify itself would make
/// every badge on the platform worthless, which is the line ADR-002 draws and the reason this
/// interface returns a case identifier rather than a decision.
/// </para>
/// <para>
/// <b>It lives in <c>Diaspora.Contracts</c>, not in the Trust module</b>, for the same reason
/// Trust's events do: a consumer must be able to submit without referencing the module that
/// decides. B7's architecture test asserts it holds no dependency on <c>Diaspora.Modules.Trust</c>,
/// and that test still passes with this contract in use.
/// </para>
/// <para>
/// <b>Idempotency.</b> Set <see cref="TrustVerificationSubmissionRequest.CorrelationId"/> and
/// retries are free — a repeat returns the original case with
/// <see cref="TrustVerificationSubmissionResult.IsDuplicate"/> set, and opens nothing. Without
/// one, Trust still refuses to open a second live case for the same subject and type, but cannot
/// distinguish a retry from a deliberate resubmission.
/// </para>
/// <para>
/// <b>Registration.</b> The Trust module registers the implementation. A deployment that does not
/// load Trust has no verification engine at all, so a consumer should treat the dependency as
/// optional and degrade — B7's provider does exactly that, and reports the claim as undecided
/// rather than pretending it was filed.
/// </para>
/// </summary>
public interface ITrustVerificationSubmission
{
    /// <summary>
    /// Opens a verification case, or returns the one that already covers this claim.
    /// <para>
    /// A failed <see cref="Result{T}"/> means the submission was refused and no case exists —
    /// an unknown verification type, a caller submitting for somebody other than themselves, a
    /// member the engine cannot place. A successful one means Trust holds the case, whatever
    /// <see cref="TrustVerificationSubmissionResult.Status"/> says.
    /// </para>
    /// </summary>
    Task<Result<TrustVerificationSubmissionResult>> SubmitAsync(
        TrustVerificationSubmissionRequest request,
        CancellationToken cancellationToken = default);
}
