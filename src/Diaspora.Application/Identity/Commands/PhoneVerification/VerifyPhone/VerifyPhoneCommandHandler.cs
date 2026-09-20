using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Validation;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Application.Identity.Commands.PhoneVerification.VerifyPhone;

public sealed class VerifyPhoneCommandHandler : ICommandHandler<VerifyPhoneCommand, AuthenticationResponse>
{
    private readonly IUserRepository _users;
    private readonly IOtpChallengeService _otpChallenges;
    private readonly ITokenIssuanceService _tokenIssuance;
    private readonly IDeviceRegistrationService _deviceRegistration;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IRequestContext _requestContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public VerifyPhoneCommandHandler(
        IUserRepository users,
        IOtpChallengeService otpChallenges,
        ITokenIssuanceService tokenIssuance,
        IDeviceRegistrationService deviceRegistration,
        ISecurityEventWriter securityEvents,
        IRequestContext requestContext,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _otpChallenges = otpChallenges;
        _tokenIssuance = tokenIssuance;
        _deviceRegistration = deviceRegistration;
        _securityEvents = securityEvents;
        _requestContext = requestContext;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AuthenticationResponse>> Handle(
        VerifyPhoneCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!PhoneNumber.TryCreate(request.PhoneNumber, out var phoneNumber) || phoneNumber is null)
        {
            return Result.Failure<AuthenticationResponse>(OtpErrors.IncorrectCode);
        }

        var user = await _users.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            // Deliberately the answer a wrong code produces.
            //
            // This used to answer `NotFound`, under a comment saying that matched "an expired or
            // absent challenge, so this endpoint cannot be used to discover which numbers are
            // registered". It matched an *absent* one. A registered number with a live challenge
            // answered `IncorrectCode` to the same guess — so a wrong code told a caller whether
            // the number had a verification outstanding, which is to say whether it had just
            // registered. The sibling defect on sign-in was closed on 17 Sep 2026 and on password
            // reset on 20 Sep; this is the third of the same shape.
            return Result.Failure<AuthenticationResponse>(OtpErrors.IncorrectCode);
        }

        // The state of a challenge is told to whoever typed the code it sent, and "incorrect" to
        // everybody else. A member whose code expired still hears that it expired, because they
        // hold it.
        var verification = await _otpChallenges.VerifyForAnonymousCallerAsync(
            user, OtpPurpose.PhoneVerification, request.Code, cancellationToken);

        if (verification.IsFailure)
        {
            await _securityEvents.WriteAsync(new SecurityEventDraft
            {
                UserId = user.Id,
                Type = verification.Error == OtpErrors.AttemptLimitReached
                    ? SecurityEventType.OtpAttemptLimitReached
                    : SecurityEventType.OtpVerificationFailed,
                Severity = SecurityEventSeverity.Warning,
                Description = verification.Error.Code
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthenticationResponse>(verification.Error);
        }

        var now = _clock.UtcNow;

        var confirmation = user.ConfirmPhoneVerified(now);
        if (confirmation.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(confirmation.Error);
        }

        var platform = IdentityValueParser.ParsePlatform(request.Platform);

        var deviceId = await _deviceRegistration.ResolveAsync(
            user, request.DeviceIdentifier, request.DeviceName, platform, request.AppVersion, cancellationToken);

        var issuance = await _tokenIssuance.IssueForNewSessionAsync(
            user,
            new SessionCreationContext
            {
                DeviceId = deviceId,
                DeviceIdentifier = request.DeviceIdentifier,
                DeviceName = request.DeviceName,
                Platform = platform,
                AppVersion = request.AppVersion,
                IpAddress = _requestContext.IpAddress,
                UserAgent = _requestContext.UserAgent
            },
            cancellationToken);

        if (issuance.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(issuance.Error);
        }

        var outcome = issuance.Value;

        user.RecordSuccessfulLogin(outcome.Session.Id, now);
        _users.Update(user);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            SessionId = outcome.Session.Id,
            Type = SecurityEventType.PhoneVerified,
            Severity = SecurityEventSeverity.Informational,
            Description = "Phone number verified; promoted to limited access."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TokenMapper.ToAuthenticationResponse(outcome, user));
    }
}
