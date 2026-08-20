using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Authentication.Logout;

/// <summary>
/// Revokes the caller's session and, optionally, every other session on the account.
/// </summary>
public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand, OperationAcknowledgedResponse>
{
    private readonly IUserSessionRepository _sessions;
    private readonly ISessionRevocationService _revocation;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public LogoutCommandHandler(
        IUserSessionRepository sessions,
        ISessionRevocationService revocation,
        ICurrentIdentityContext currentIdentity,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _sessions = sessions;
        _revocation = revocation;
        _currentIdentity = currentIdentity;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        LogoutCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;
        var sessionId = _currentIdentity.SessionId;

        if (userId is null || sessionId is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(SessionErrors.NotActive);
        }

        var now = _clock.UtcNow;

        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            return Result.Failure<OperationAcknowledgedResponse>(SessionErrors.NotFound);
        }

        // Already revoked is not an error worth failing a sign-out over — the caller
        // wanted the session gone, and it is.
        if (session.Revoke(SessionRevocationReason.SignedOut, now).IsSuccess)
        {
            _sessions.Update(session);
        }

        var alsoRevoked = 0;
        if (request.RevokeAllSessions)
        {
            alsoRevoked = await _revocation.RevokeAllExceptAsync(
                userId, sessionId, SessionRevocationReason.RevokedByUserBulk, now, cancellationToken);
        }

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = userId,
            SessionId = sessionId,
            Type = SecurityEventType.LogoutSucceeded,
            Severity = SecurityEventSeverity.Informational,
            Description = request.RevokeAllSessions
                ? $"Signed out and revoked {alsoRevoked} other session(s)."
                : "Signed out."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OperationAcknowledgedResponse.Create(
            request.RevokeAllSessions
                ? "You have been signed out on this and all other devices."
                : "You have been signed out."));
    }
}
