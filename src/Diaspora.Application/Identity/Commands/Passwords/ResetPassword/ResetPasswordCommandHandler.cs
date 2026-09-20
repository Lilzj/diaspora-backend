using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Application.Identity.Commands.Passwords.ResetPassword;

/// <summary>
/// Resets a password against a verified recovery code.
/// <para>
/// Every session on the account is revoked, with no exception for the caller: whoever is
/// resetting has not proved they are on a trusted device, and the whole point of recovery
/// is that the previous credential may be in someone else's hands.
/// </para>
/// <para>
/// A successful recovery also clears a security hard lock, since account recovery is the
/// documented route out of <c>Locked</c>.
/// </para>
/// </summary>
public sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, OperationAcknowledgedResponse>
{
    private readonly IUserRepository _users;
    private readonly IOtpChallengeService _otpChallenges;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicy _passwordPolicy;
    private readonly ISessionRevocationService _revocation;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ResetPasswordCommandHandler(
        IUserRepository users,
        IOtpChallengeService otpChallenges,
        IPasswordHasher passwordHasher,
        IPasswordPolicy passwordPolicy,
        ISessionRevocationService revocation,
        ISecurityEventWriter securityEvents,
        IIdentityPolicyProvider policies,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _otpChallenges = otpChallenges;
        _passwordHasher = passwordHasher;
        _passwordPolicy = passwordPolicy;
        _revocation = revocation;
        _securityEvents = securityEvents;
        _policies = policies;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Identified by the same address the recovery was requested with. The history
        // is loaded because the policy below refuses a password the member has used
        // before, and a history-less load would let that refusal silently never fire.
        //
        // **Every miss answers identically, and that is the point.** This route is
        // anonymous, and it used to answer `NotFound` for an address with no account and
        // the challenge's own state — "expired", "already used", "too many attempts" —
        // for one that had. So anybody could ask it whether an email address was
        // registered, without a code and without a message ever reaching the member. The
        // same defect was found on the OTP sign-in route on 17 Sep 2026; this is the same
        // treatment, and `PhoneOtpAuthenticationProvider` is the shape it copies.
        if (!EmailAddress.TryCreate(request.Email, out var email) || email is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(OtpErrors.IncorrectCode);
        }

        var user = await _users.GetByEmailWithPasswordHistoryAsync(email, cancellationToken);
        if (user is null)
        {
            // Deliberately the answer a wrong code produces. Anything more specific says
            // "that address is not registered", which is the enumeration this must refuse.
            return Result.Failure<OperationAcknowledgedResponse>(OtpErrors.IncorrectCode);
        }

        // Reaching here says only that the address is registered, which the caller must not
        // learn. The state of a challenge is told to whoever typed the code it sent, and
        // "incorrect" to everybody else.
        var verification = await _otpChallenges.VerifyForAnonymousCallerAsync(
            user, OtpPurpose.PasswordReset, request.Code, cancellationToken);

        if (verification.IsFailure)
        {
            await _securityEvents.WriteAsync(new SecurityEventDraft
            {
                UserId = user.Id,
                Type = SecurityEventType.OtpVerificationFailed,
                Severity = SecurityEventSeverity.Warning,
                Description = $"Recovery code rejected: {verification.Error.Code}"
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<OperationAcknowledgedResponse>(verification.Error);
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

        var historyPolicy = _policies.Current.PasswordHistory;
        if (historyPolicy.Enforced &&
            (user.MatchesCurrentPassword(hash =>
                 _passwordHasher.Verify(hash, request.NewPassword) != PasswordVerificationOutcome.Failed) ||
             user.MatchesHistoricalPassword(hash =>
                 _passwordHasher.Verify(hash, request.NewPassword) != PasswordVerificationOutcome.Failed)))
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.PasswordPreviouslyUsed);
        }

        var now = _clock.UtcNow;

        var change = user.ChangePassword(
            _passwordHasher.Hash(request.NewPassword), now, historyPolicy, wasReset: true);

        if (change.IsFailure)
        {
            return Result.Failure<OperationAcknowledgedResponse>(change.Error);
        }

        // Recovery is the documented way out of a security hard lock.
        if (user.AccessState == AccessState.Locked)
        {
            user.Reinstate(now, AccessStateChangeReason.AccountRecovery);
        }

        _users.Update(user);

        var revoked = await _revocation.RevokeAllAsync(
            user.Id, SessionRevocationReason.PasswordReset, now, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.PasswordResetCompleted,
            Severity = SecurityEventSeverity.Notice,
            Description = $"Password reset through account recovery; {revoked} session(s) revoked."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OperationAcknowledgedResponse.Create(
            "Your password has been reset. Sign in with your new password."));
    }
}
