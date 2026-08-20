using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Commands.Authentication.RequestSignInOtp;

/// <summary>
/// Issues a sign-in code.
/// <para>
/// Modelled on <c>ForgotPassword</c>, and for the same reason: the endpoint is anonymous
/// and takes only a phone number, so it must return an identical acknowledgement whether or
/// not the number belongs to an account, and whether or not a code could be sent. Any
/// variation would make it an account-enumeration oracle. A member who cannot authenticate
/// at all — locked, disabled — is quietly not sent a code, but the caller is told the same
/// thing regardless.
/// </para>
/// </summary>
public sealed class RequestSignInOtpCommandHandler
    : ICommandHandler<RequestSignInOtpCommand, OperationAcknowledgedResponse>
{
    private const string Acknowledgement =
        "If that phone number belongs to a Diaspora account, we have sent a sign-in code to it.";

    private readonly IUserRepository _users;
    private readonly IOtpChallengeService _otpChallenges;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<RequestSignInOtpCommandHandler> _logger;

    public RequestSignInOtpCommandHandler(
        IUserRepository users,
        IOtpChallengeService otpChallenges,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<RequestSignInOtpCommandHandler> logger)
    {
        _users = users;
        _otpChallenges = otpChallenges;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        RequestSignInOtpCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!PhoneNumber.TryCreate(request.PhoneNumber, out var phoneNumber) || phoneNumber is null)
        {
            return Acknowledge();
        }

        var user = await _users.GetByPhoneNumberAsync(phoneNumber, cancellationToken);

        // Only send a code to an account that could actually complete a sign-in. A locked
        // or disabled account gets nothing — but the caller cannot tell that apart from an
        // unregistered number.
        if (user is not null && user.EnsureCanAuthenticate(_clock.UtcNow).IsSuccess)
        {
            var challenge = await _otpChallenges.IssueAsync(user, OtpPurpose.SignIn, cancellationToken);
            if (challenge.IsFailure)
            {
                _logger.LogWarning(
                    "Could not issue a sign-in code for user {UserId}: {Error}",
                    user.Id, challenge.Error.Code);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Acknowledge();
    }

    private static Result<OperationAcknowledgedResponse> Acknowledge() =>
        Result.Success(OperationAcknowledgedResponse.Create(Acknowledgement));
}
