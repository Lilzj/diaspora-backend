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

namespace Diaspora.Application.Identity.Commands.Lifecycle.SuspendAccount;

/// <summary>
/// Suspends an account and cuts its live sessions.
/// <para>
/// The member can still sign in afterwards — the Access Gateway routes them to the
/// suspension screen — but the tokens they were holding stop working immediately. A
/// suspension that only took effect at the next token expiry would not be a suspension.
/// </para>
/// </summary>
public sealed class SuspendAccountCommandHandler : ICommandHandler<SuspendAccountCommand, OperationAcknowledgedResponse>
{
    private readonly IUserRepository _users;
    private readonly ISessionRevocationService _revocation;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SuspendAccountCommandHandler(
        IUserRepository users,
        ISessionRevocationService revocation,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _revocation = revocation;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        SuspendAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _users.GetByIdAsync(UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.NotFound);
        }

        var now = _clock.UtcNow;

        var suspension = user.Suspend(request.Reason, request.SuspendedUntil, now);
        if (suspension.IsFailure)
        {
            return Result.Failure<OperationAcknowledgedResponse>(suspension.Error);
        }

        _users.Update(user);

        var revoked = await _revocation.RevokeAllAsync(
            user.Id, SessionRevocationReason.AccountStateChanged, now, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.AccountSuspended,
            Severity = SecurityEventSeverity.Warning,
            Description = $"Account suspended; {revoked} session(s) revoked.",
            Metadata = new Dictionary<string, string>
            {
                ["reason"] = request.Reason,
                ["until"] = request.SuspendedUntil?.ToString("O") ?? "indefinite"
            }
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OperationAcknowledgedResponse.Create("The account has been suspended."));
    }
}
