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

namespace Diaspora.Application.Identity.Commands.Passwords.ChangePassword;

/// <summary>
/// Changes a password and cuts every other session.
/// <para>
/// The caller's own session survives — signing someone out of the device they just used
/// to secure their account is hostile. Every other session goes, because a password
/// change is the standard response to "I think someone else is in my account", and
/// leaving those sessions alive would defeat the point.
/// </para>
/// </summary>
public sealed class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand, OperationAcknowledgedResponse>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicy _passwordPolicy;
    private readonly ISessionRevocationService _revocation;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ChangePasswordCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IPasswordPolicy passwordPolicy,
        ISessionRevocationService revocation,
        ICurrentIdentityContext currentIdentity,
        ISecurityEventWriter securityEvents,
        IIdentityPolicyProvider policies,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _passwordPolicy = passwordPolicy;
        _revocation = revocation;
        _currentIdentity = currentIdentity;
        _securityEvents = securityEvents;
        _policies = policies;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdWithPasswordHistoryAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.NotFound);
        }

        var currentMatches = _passwordHasher.Verify(user.PasswordHash, request.CurrentPassword);
        if (currentMatches == PasswordVerificationOutcome.Failed)
        {
            await _securityEvents.WriteAsync(new SecurityEventDraft
            {
                UserId = user.Id,
                Type = SecurityEventType.LoginFailed,
                Severity = SecurityEventSeverity.Warning,
                Description = "Password change attempted with an incorrect current password."
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.IncorrectCurrentPassword);
        }

        var policyCheck = _passwordPolicy.Validate(request.NewPassword, new PasswordPolicyContext
        {
            PhoneNumber = user.PhoneNumber.Value,
            FullName = user.FullName.Value,
            Email = user.Email?.Value
        });

        if (policyCheck.IsFailure)
        {
            return Result.Failure<OperationAcknowledgedResponse>(policyCheck.Error);
        }

        if (_passwordHasher.Verify(user.PasswordHash, request.NewPassword) != PasswordVerificationOutcome.Failed)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.SamePassword);
        }

        var historyPolicy = _policies.Current.PasswordHistory;
        if (historyPolicy.Enforced &&
            user.MatchesHistoricalPassword(hash =>
                _passwordHasher.Verify(hash, request.NewPassword) != PasswordVerificationOutcome.Failed))
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.PasswordPreviouslyUsed);
        }

        var now = _clock.UtcNow;

        var change = user.ChangePassword(_passwordHasher.Hash(request.NewPassword), now, historyPolicy);
        if (change.IsFailure)
        {
            return Result.Failure<OperationAcknowledgedResponse>(change.Error);
        }

        _users.Update(user);

        var revoked = _currentIdentity.SessionId is { } currentSession
            ? await _revocation.RevokeAllExceptAsync(
                user.Id, currentSession, SessionRevocationReason.PasswordChanged, now, cancellationToken)
            : await _revocation.RevokeAllAsync(
                user.Id, SessionRevocationReason.PasswordChanged, now, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            SessionId = _currentIdentity.SessionId,
            Type = SecurityEventType.PasswordChanged,
            Severity = SecurityEventSeverity.Notice,
            Description = $"Password changed; {revoked} other session(s) revoked."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OperationAcknowledgedResponse.Create(
            revoked > 0
                ? $"Your password has been changed. {revoked} other session(s) were signed out."
                : "Your password has been changed."));
    }
}
