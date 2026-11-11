using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.StepUp;

/// <summary>
/// Issues a step-up code to the signed-in member's registered phone.
/// <para>
/// <b>To the phone, never to the email, and that is not this handler's choice.</b>
/// <c>EmailOtpDeliveryService</c> excludes <see cref="OtpPurpose.StepUpAuthentication"/> from email
/// for a reason worth repeating: a code sent to the mailbox that would also receive a password
/// reset is not a second factor, because whoever holds the mailbox holds both. BSEC-007's
/// remediation offers "phone or email"; the platform is right and the finding is not.
/// </para>
/// <para>
/// <b>Nothing here is conditional on the member having been refused first.</b> A client that knows
/// a withdrawal is coming can collect the code before showing the amount screen, which is a better
/// flow than a refusal the member has to understand. The grant is useless without the redemption,
/// so an unused code costs an SMS and nothing else — and the OTP service's own issue budget is
/// what keeps that from being abused.
/// </para>
/// </summary>
public sealed class RequestStepUpCommandHandler
    : ICommandHandler<RequestStepUpCommand, OtpChallengeResponse>
{
    private readonly IUserRepository _users;
    private readonly IOtpChallengeService _otpChallenges;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RequestStepUpCommandHandler(
        IUserRepository users,
        IOtpChallengeService otpChallenges,
        ICurrentIdentityContext currentIdentity,
        ISecurityEventWriter securityEvents,
        IIdentityPolicyProvider policies,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _otpChallenges = otpChallenges;
        _currentIdentity = currentIdentity;
        _securityEvents = securityEvents;
        _policies = policies;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OtpChallengeResponse>> Handle(
        RequestStepUpCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentIdentity.UserId is not { } userId)
        {
            return Result.Failure<OtpChallengeResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<OtpChallengeResponse>(UserErrors.InvalidCredentials);
        }

        var challenge = await _otpChallenges.IssueAsync(
            user, OtpPurpose.StepUpAuthentication, cancellationToken);

        if (challenge.IsFailure)
        {
            // Saved before returning, because a failed issue still consumed budget and still
            // superseded the outstanding challenge — losing that would hand a caller an unlimited
            // supply of attempts by failing delivery on purpose.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<OtpChallengeResponse>(challenge.Error);
        }

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.OtpIssued,
            Severity = SecurityEventSeverity.Informational,
            SessionId = _currentIdentity.SessionId,

            // The scope, because "a code was sent" and "a code was sent to approve a payout" are
            // different entries to whoever reads this table after a disputed withdrawal.
            Description = $"Step-up code issued for {request.Scope}."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OtpChallengeResponseFactory.Create(
            challenge.Value, _clock.UtcNow, _policies.Current.Otp.ResendCooldown));
    }
}
