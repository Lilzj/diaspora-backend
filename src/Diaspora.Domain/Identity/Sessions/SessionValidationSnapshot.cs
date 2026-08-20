using Diaspora.SharedKernel.Identity;

namespace Diaspora.Domain.Identity.Sessions;

/// <summary>
/// The three facts the per-request token validator needs about a session, and nothing else.
/// <para>
/// The audit's largest performance finding: validation loaded the whole <c>UserSession</c>
/// aggregate — <c>Include(RefreshTokens)</c> and all — on every authenticated request, so
/// the cost grew with the length of the rotation chain, which grows with the age of the
/// session. This projection is a single indexed row with no navigation and no change
/// tracking. The full aggregate is still loaded on the rotation path, which is the only
/// place that mutates it.
/// </para>
/// </summary>
public sealed record SessionValidationSnapshot(
    SessionId SessionId,
    UserId UserId,
    SessionStatus Status,
    DateTimeOffset AbsoluteExpiresAt)
{
    public bool IsActiveAt(DateTimeOffset now) =>
        Status == SessionStatus.Active && AbsoluteExpiresAt > now;
}
