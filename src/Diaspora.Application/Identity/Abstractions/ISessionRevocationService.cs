using Diaspora.Domain.Identity.Sessions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Bulk session termination.
/// <para>
/// Sessions are their own aggregate, so "revoke everything" cannot be a method on
/// <c>User</c>. Naming the operation here keeps the intent explicit at every call site
/// that needs it — password change, reset, lock, suspend, disable.
/// </para>
/// </summary>
public interface ISessionRevocationService
{
    /// <summary>Revokes every active session for a member.</summary>
    Task<int> RevokeAllAsync(
        UserId userId,
        SessionRevocationReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes every active session except the one supplied.</summary>
    Task<int> RevokeAllExceptAsync(
        UserId userId,
        SessionId exceptSessionId,
        SessionRevocationReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
