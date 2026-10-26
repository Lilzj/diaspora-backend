using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Commands.Registration;

/// <summary>
/// Creates the account, then dispatches the first verification code.
/// <para>
/// The two steps are ordered so a failure to send SMS leaves the member in
/// <c>RegistrationStarted</c> — visibly incomplete, and recoverable by requesting a code —
/// rather than in <c>PhoneVerificationPending</c>, which would claim a code is waiting
/// that never arrived.
/// </para>
/// </summary>
public sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, RegistrationResponse>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicy _passwordPolicy;
    private readonly IOtpChallengeService _otpChallenges;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IPasswordPolicy passwordPolicy,
        IOtpChallengeService otpChallenges,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _passwordPolicy = passwordPolicy;
        _otpChallenges = otpChallenges;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<RegistrationResponse>> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!PhoneNumber.TryCreate(request.PhoneNumber, out var phoneNumber) || phoneNumber is null)
        {
            return Result.Failure<RegistrationResponse>(Error.Validation(
                nameof(request.PhoneNumber), "Enter a valid international phone number."));
        }

        if (await _users.ExistsByPhoneNumberAsync(phoneNumber, cancellationToken))
        {
            return Result.Failure<RegistrationResponse>(UserErrors.PhoneAlreadyRegistered);
        }

        // ── The email is now a credential, not a profile field ────────────────────
        //
        // Sign-in moved from a phone one-time code to an email address and a password,
        // so this is the identifier the member will use for the rest of their life on
        // the platform. It is required, it is validated here rather than at the wire,
        // and it has to be unique — two accounts sharing an address would make
        // `GetByEmailAsync` ambiguous and sign-in non-deterministic.
        if (!EmailAddress.TryCreate(request.Email, out var email) || email is null)
        {
            return Result.Failure<RegistrationResponse>(Error.Validation(
                nameof(request.Email), "Enter a valid email address."));
        }

        if (await _users.ExistsByEmailAsync(email, cancellationToken))
        {
            return Result.Failure<RegistrationResponse>(UserErrors.EmailAlreadyRegistered);
        }

        if (!FullName.TryCreate(request.FullName, out var fullName) || fullName is null ||
            !CountryCode.TryCreate(request.Nationality, out var nationality) || nationality is null ||
            !CountryCode.TryCreate(request.CountryOfResidence, out var residence) || residence is null)
        {
            return Result.Failure<RegistrationResponse>(Error.Validation(
                "Identity.Registration.InvalidProfile", "One or more profile fields are invalid."));
        }

        var language = LanguageCode.TryCreate(request.PreferredLanguage, out var parsedLanguage) && parsedLanguage is not null
            ? parsedLanguage
            : LanguageCode.Default;

        var timeZone = TimeZoneIdentifier.TryCreate(request.PreferredTimeZone, out var parsedTimeZone) && parsedTimeZone is not null
            ? parsedTimeZone
            : TimeZoneIdentifier.Utc;

        var policyCheck = _passwordPolicy.Validate(request.Password, new PasswordPolicyContext
        {
            PhoneNumber = phoneNumber.Value,
            FullName = fullName.Value,
            // The address is the sign-in identifier now, so a password derived from it
            // is the first thing an attacker would try.
            Email = email.Value
        });

        if (policyCheck.IsFailure)
        {
            return Result.Failure<RegistrationResponse>(policyCheck.Error);
        }

        var now = _clock.UtcNow;

        var user = User.Register(
            fullName,
            phoneNumber,
            _passwordHasher.Hash(request.Password),
            nationality,
            residence,
            language,
            timeZone,
            now,
            request.ReferralCode);

        // `User.Register` predates email being a credential and does not take one.
        // Set through the domain so the profile-updated event is raised exactly as it
        // would be for any later change.
        user.SetEmail(email, now);

        await _users.AddAsync(user, cancellationToken);

        var challenge = await _otpChallenges.IssueAsync(
            user, OtpPurpose.PhoneVerification, cancellationToken);

        if (challenge.IsFailure)
        {
            // The account is kept: the member can request a code from the resend endpoint
            // without re-entering everything, and the number stays reserved to them.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Registered user {UserId} but could not deliver the verification code: {Error}",
                user.Id, challenge.Error.Code);

            return Result.Failure<RegistrationResponse>(challenge.Error);
        }

        var beginVerification = user.BeginPhoneVerification(now);
        if (beginVerification.IsFailure)
        {
            return Result.Failure<RegistrationResponse>(beginVerification.Error);
        }

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registered user {UserId} from {Country}", user.Id, residence.Value);

        return Result.Success(new RegistrationResponse
        {
            UserId = user.Id.Value,
            PhoneNumber = phoneNumber.Masked,
            AccessState = user.AccessState.Name,
            Verification = OtpChallengeResponseFactory.Create(challenge.Value, now)
        });
    }
}
