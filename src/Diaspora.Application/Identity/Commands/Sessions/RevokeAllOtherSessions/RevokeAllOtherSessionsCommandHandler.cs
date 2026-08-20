using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Sessions.RevokeAllOtherSessions;

public sealed class RevokeAllOtherSessionsCommandHandler
    : ICommandHandler<RevokeAllOtherSessionsCommand, OperationAcknowledgedResponse>
{
    private readonly ISessionRevocationService _revocation;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RevokeAllOtherSessionsCommandHandler(
        ISessionRevocationService revocation,
        ICurrentIdentityContext currentIdentity,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _revocation = revocation;
        _currentIdentity = currentIdentity;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        RevokeAllOtherSessionsCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentIdentity.UserId;
        var sessionId = _currentIdentity.SessionId;

        if (userId is null || sessionId is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.InvalidCredentials);
        }

        var revoked = await _revocation.RevokeAllExceptAsync(
            userId, sessionId, SessionRevocationReason.RevokedByUserBulk, _clock.UtcNow, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = userId,
            SessionId = sessionId,
            Type = SecurityEventType.AllSessionsRevoked,
            Severity = SecurityEventSeverity.Notice,
            Description = $"{revoked} other session(s) revoked by the member."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OperationAcknowledgedResponse.Create(
            revoked == 0
                ? "There were no other active sessions to sign out."
                : $"{revoked} other session(s) have been signed out."));
    }
}
