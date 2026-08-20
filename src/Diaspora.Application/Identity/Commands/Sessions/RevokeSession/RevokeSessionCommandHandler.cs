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
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Sessions.RevokeSession;

public sealed class RevokeSessionCommandHandler : ICommandHandler<RevokeSessionCommand, OperationAcknowledgedResponse>
{
    private readonly IUserSessionRepository _sessions;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RevokeSessionCommandHandler(
        IUserSessionRepository sessions,
        ICurrentIdentityContext currentIdentity,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _sessions = sessions;
        _currentIdentity = currentIdentity;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        RevokeSessionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.InvalidCredentials);
        }

        var sessionId = SessionId.From(request.SessionId);

        if (_currentIdentity.SessionId == sessionId)
        {
            // Revoking the session you are holding would leave the client with tokens it
            // cannot use and no clear signal why. Sign-out is the operation for that.
            return Result.Failure<OperationAcknowledgedResponse>(SessionErrors.CannotRevokeCurrentSession);
        }

        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(SessionErrors.NotFound);
        }

        if (session.UserId != userId)
        {
            // Deliberately the same "not found" a missing session gives, so the endpoint
            // cannot be used to probe for other members' session identifiers.
            return Result.Failure<OperationAcknowledgedResponse>(SessionErrors.NotFound);
        }

        var revocation = session.Revoke(SessionRevocationReason.RevokedByUser, _clock.UtcNow);
        if (revocation.IsFailure)
        {
            return Result.Failure<OperationAcknowledgedResponse>(revocation.Error);
        }

        _sessions.Update(session);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = userId,
            SessionId = sessionId,
            Type = SecurityEventType.SessionRevoked,
            Severity = SecurityEventSeverity.Notice,
            Description = $"Session on {session.DeviceName} revoked by the member."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OperationAcknowledgedResponse.Create(
            $"The session on {session.DeviceName} has been signed out."));
    }
}
