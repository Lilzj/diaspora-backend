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
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Application.Identity.Commands.PhoneVerification.ResendOtp;

/// <summary>
/// Resends a one-time code.
/// <para>
/// An unknown phone number receives the same shaped response as a known one. The endpoint
/// is unauthenticated, so any difference in behaviour would turn it into an account
/// enumeration oracle — and the throttle in the OTP service is what keeps that from being
/// abused as a free SMS pump.
/// </para>
/// </summary>
public sealed class ResendOtpCommandHandler : ICommandHandler<ResendOtpCommand, OtpChallengeResponse>
{
    private readonly IUserRepository _users;
    private readonly IOtpChallengeService _otpChallenges;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ResendOtpCommandHandler(
        IUserRepository users,
        IOtpChallengeService otpChallenges,
        ISecurityEventWriter securityEvents,
        IIdentityPolicyProvider policies,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _otpChallenges = otpChallenges;
        _securityEvents = securityEvents;
        _policies = policies;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OtpChallengeResponse>> Handle(
        ResendOtpCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The validator has already refused an unrecognised purpose, so the parse cannot
        // fail here; on the defensive path it falls back to phone verification.
        _ = IdentityValueParser.TryParseOtpPurpose(request.Purpose, out var purpose);

        if (!PhoneNumber.TryCreate(request.PhoneNumber, out var phoneNumber) || phoneNumber is null)
        {
            return Result.Success(DecoyResponse(request.PhoneNumber, purpose.ToString()));
        }

        var user = await _users.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            return Result.Success(DecoyResponse(phoneNumber.Masked, purpose.ToString()));
        }

        var challenge = await _otpChallenges.ResendAsync(user, purpose, cancellationToken);
        if (challenge.IsFailure)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<OtpChallengeResponse>(challenge.Error);
        }

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.OtpIssued,
            Severity = SecurityEventSeverity.Informational,
            Description = $"Verification code resent for {purpose}."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OtpChallengeResponseFactory.Create(
            challenge.Value, _clock.UtcNow, _policies.Current.Otp.ResendCooldown));
    }

    /// <summary>
    /// The response an unregistered number gets: correctly shaped, and describing a
    /// challenge that does not exist.
    /// </summary>
    private OtpChallengeResponse DecoyResponse(string phoneNumber, string purpose)
    {
        var policy = _policies.Current.Otp;

        return new OtpChallengeResponse
        {
            PhoneNumber = phoneNumber,
            Purpose = purpose,
            ExpiresAt = _clock.UtcNow.Add(policy.Lifetime),
            AttemptsRemaining = policy.MaxVerificationAttempts,
            ResendsRemaining = policy.MaxResends,
            ResendAvailableInSeconds = (int)policy.ResendCooldown.TotalSeconds
        };
    }
}
