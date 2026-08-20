using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Assembles a complete authentication response: opens or rotates a session, mints the
/// token pair and attaches the access decision.
/// <para>
/// Sign-in, phone verification and password change all need this exact sequence. Doing
/// it once here is what stops three subtly different versions of token issuance from
/// drifting apart.
/// </para>
/// </summary>
public interface ITokenIssuanceService
{
    /// <summary>Opens a new session for a member and issues the first token pair.</summary>
    Task<Result<AuthenticationOutcome>> IssueForNewSessionAsync(
        User user,
        SessionCreationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Issues a token pair for a session that already exists, after rotation.</summary>
    Result<AuthenticationOutcome> IssueForExistingSession(
        User user,
        UserSession session,
        RefreshTokenDescriptor rotatedRefreshToken);
}
