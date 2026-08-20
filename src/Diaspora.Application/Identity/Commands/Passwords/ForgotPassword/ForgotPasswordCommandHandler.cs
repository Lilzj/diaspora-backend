using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Commands.Passwords.ForgotPassword;

/// <summary>
/// Issues a recovery code.
/// <para>
/// Always returns the same acknowledgement, whether or not the number belongs to an
/// account, and whether or not the code could be sent. This endpoint is unauthenticated
/// and takes only a phone number; any variation in the response would make it a free
/// account-enumeration tool. Real failures go to the log and the security event stream,
/// where the member cannot see them but an operator can.
/// </para>
/// </summary>
public sealed class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, OperationAcknowledgedResponse>
{
    private const string Acknowledgement =
        "If that phone number belongs to a Diaspora account, we have sent a recovery code to it.";

    private readonly IUserRepository _users;
    private readonly IOtpChallengeService _otpChallenges;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IUserRepository users,
        IOtpChallengeService otpChallenges,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _users = users;
        _otpChallenges = otpChallenges;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!PhoneNumber.TryCreate(request.PhoneNumber, out var phoneNumber) || phoneNumber is null)
        {
            return Acknowledge();
        }

        var user = await _users.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            _logger.LogInformation("Recovery requested for an unregistered phone number.");
            return Acknowledge();
        }

        var challenge = await _otpChallenges.IssueAsync(user, OtpPurpose.PasswordReset, cancellationToken);
        if (challenge.IsFailure)
        {
            _logger.LogWarning(
                "Could not issue a recovery code for user {UserId}: {Error}", user.Id, challenge.Error.Code);
        }
        else
        {
            await _securityEvents.WriteAsync(new SecurityEventDraft
            {
                UserId = user.Id,
                Type = SecurityEventType.PasswordResetRequested,
                Severity = SecurityEventSeverity.Notice,
                Description = "Account recovery code issued."
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Acknowledge();
    }

    private static Result<OperationAcknowledgedResponse> Acknowledge() =>
        Result.Success(OperationAcknowledgedResponse.Create(Acknowledgement));
}
