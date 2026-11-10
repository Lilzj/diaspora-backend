using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Issues and checks proofs that the session in front of us is the member's (BSEC-007).
/// <para>
/// One service rather than a repository behind the gate, for the reason the platform keeps
/// learning: the rules are the control. Which claim names the session, what a missing claim means,
/// and whether a grant has expired are three decisions that have to agree between the gate that
/// refuses and the handler that grants, and a repository would leave each caller to make them
/// again.
/// </para>
/// </summary>
public interface IStepUpService
{
    /// <summary>
    /// Whether the caller has already proved themselves for <paramref name="scope"/>.
    /// <para>
    /// <b>Every uncertain answer is false.</b> No authenticated user, no session claim, a session
    /// claim that is not a GUID, an unknown scope — each returns false and the request is refused.
    /// A control that admits the requests it cannot classify protects nothing, and a token with no
    /// <c>sid</c> is a service token, which has no business requesting a member's payout.
    /// </para>
    /// </summary>
    Task<bool> HasProvenAsync(string scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the caller's session proved itself for <paramref name="scope"/>, having just
    /// answered <paramref name="challengeId" /> correctly.
    /// <para>
    /// Fails rather than granting when the caller has no session to bind to. The grant is written
    /// to the unit of work; the caller commits it.
    /// </para>
    /// </summary>
    Task<Result<DateTimeOffset>> GrantAsync(
        User user,
        string scope,
        SharedKernel.Identity.OtpChallengeId challengeId,
        CancellationToken cancellationToken = default);
}
