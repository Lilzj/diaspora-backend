using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Domain.Identity.Repositories;

/// <summary>
/// Stores step-up grants and answers the one question the gate asks.
/// <para>
/// Two operations and no update, because <see cref="StepUpGrant"/> is append-only. There is
/// deliberately no "get the grant" — a caller holding a grant object would be tempted to decide
/// for itself whether it is still live, and the comparison against the clock belongs in one place.
/// </para>
/// </summary>
public interface IStepUpGrantRepository
{
    Task AddAsync(StepUpGrant grant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="sessionId"/> holds a grant for <paramref name="scope"/> that has not
    /// expired as of <paramref name="asOf"/>.
    /// <para>
    /// All four arguments narrow it, and dropping any one of them breaks the control: without the
    /// user a grant authorises another member's payout, without the session a stolen token rides
    /// the member's own window, without the scope adding a destination authorises a withdrawal, and
    /// without the clock a grant never expires.
    /// </para>
    /// </summary>
    Task<bool> HasLiveGrantAsync(
        UserId userId,
        SessionId sessionId,
        string scope,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);
}
