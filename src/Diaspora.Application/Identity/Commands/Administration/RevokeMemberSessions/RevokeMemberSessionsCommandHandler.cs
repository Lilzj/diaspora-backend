using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Administration;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Administration.RevokeMemberSessions;

public sealed class RevokeMemberSessionsCommandHandler
    : ICommandHandler<RevokeMemberSessionsCommand, OperationAcknowledgedResponse>
{
    private readonly StaffAuthority _authority;
    private readonly ISessionRevocationService _revocation;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RevokeMemberSessionsCommandHandler(
        StaffAuthority authority,
        ISessionRevocationService revocation,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _authority = authority;
        _revocation = revocation;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        RevokeMemberSessionsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolved = await _authority.ResolveAsync(UserId.From(request.UserId), cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<OperationAcknowledgedResponse>(resolved.Error);
        }

        var (actor, user) = resolved.Value;
        var now = _clock.UtcNow;
        var reason = request.Reason!.Trim();

        var revoked = await _revocation.RevokeAllAsync(
            user.Id, SessionRevocationReason.AdministrativeAction, now, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.AllSessionsRevoked,
            Severity = SecurityEventSeverity.Warning,
            Description = $"Signed out of every device by staff: {reason}",
            Metadata = new Dictionary<string, string>
            {
                ["sessionsRevoked"] = revoked.ToString(CultureInfo.InvariantCulture),
                ["revokedBy"] = actor.Id.Value.ToString(),
                ["reason"] = reason
            }
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OperationAcknowledgedResponse.Create(
            revoked == 1
                ? "One session was signed out."
                : $"{revoked.ToString(CultureInfo.InvariantCulture)} sessions were signed out."));
    }
}
