using System.Globalization;
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

namespace Diaspora.Application.Identity.Commands.Administration.RevokeMemberSessions;

public sealed class RevokeMemberSessionsCommandHandler
    : ICommandHandler<RevokeMemberSessionsCommand, OperationAcknowledgedResponse>
{
    private readonly IUserRepository _users;
    private readonly ISessionRevocationService _revocation;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RevokeMemberSessionsCommandHandler(
        IUserRepository users,
        ISessionRevocationService revocation,
        ISecurityEventWriter securityEvents,
        ICurrentIdentityContext currentIdentity,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _revocation = revocation;
        _securityEvents = securityEvents;
        _currentIdentity = currentIdentity;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        RevokeMemberSessionsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = UserId.From(request.UserId);

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.NotFound);
        }

        var now = _clock.UtcNow;

        var revoked = await _revocation.RevokeAllAsync(
            user.Id, SessionRevocationReason.AdministrativeAction, now, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.AllSessionsRevoked,
            Severity = SecurityEventSeverity.Warning,
            Description = string.IsNullOrWhiteSpace(request.Reason)
                ? "Signed out of every device by staff."
                : $"Signed out of every device by staff: {request.Reason}",
            Metadata = new Dictionary<string, string>
            {
                ["sessionsRevoked"] = revoked.ToString(CultureInfo.InvariantCulture),
                ["revokedBy"] = _currentIdentity.UserId?.Value.ToString() ?? "unknown"
            }
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OperationAcknowledgedResponse.Create(
            revoked == 1
                ? "One session was signed out."
                : $"{revoked.ToString(CultureInfo.InvariantCulture)} sessions were signed out."));
    }
}
