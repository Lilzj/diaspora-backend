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
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Commands.Authentication.Login;

/// <summary>
/// Authenticates a member.
/// <para>
/// The ordering here is a security decision. The password is verified <em>before</em> the
/// account's state is inspected, so a caller who does not know the password learns
/// nothing beyond "invalid credentials" — not whether the number is registered, not
/// whether the account is locked, suspended or merely unverified. Only once the password
/// proves the caller owns the account do state-specific errors become visible, which is
/// what makes those messages helpful rather than a probing tool.
/// </para>
/// <para>
/// When no account matches, the request still pays for one password hash against a dummy
/// value. Sign-in has to take the same time either way; otherwise the response latency
/// itself answers "is this number registered".
/// </para>
/// </summary>
public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, AuthenticationResponse>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenIssuanceService _tokenIssuance;
    private readonly IDeviceRegistrationService _deviceRegistration;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IRequestContext _requestContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenIssuanceService tokenIssuance,
        IDeviceRegistrationService deviceRegistration,
        ISecurityEventWriter securityEvents,
        IIdentityPolicyProvider policies,
        IRequestContext requestContext,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<LoginCommandHandler> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenIssuance = tokenIssuance;
        _deviceRegistration = deviceRegistration;
        _securityEvents = securityEvents;
        _policies = policies;
        _requestContext = requestContext;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AuthenticationResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;

        if (!PhoneNumber.TryCreate(request.PhoneNumber, out var phoneNumber) || phoneNumber is null)
        {
            _passwordHasher.Verify(_passwordHasher.DummyHash, request.Password);
            await RecordFailureAsync(null, "Malformed phone number.", cancellationToken);
            return Result.Failure<AuthenticationResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            _passwordHasher.Verify(_passwordHasher.DummyHash, request.Password);
            await RecordFailureAsync(null, "No account for the supplied phone number.", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthenticationResponse>(UserErrors.InvalidCredentials);
        }

        var verification = _passwordHasher.Verify(user.PasswordHash, request.Password);
        if (verification == PasswordVerificationOutcome.Failed)
        {
            user.RecordFailedLogin(now, _policies.Current.Lockout);
            _users.Update(user);

            await RecordFailureAsync(user.Id, "Incorrect password.", cancellationToken);

            if (user.Security.IsLockedOut(now))
            {
                await _securityEvents.WriteAsync(new SecurityEventDraft
                {
                    UserId = user.Id,
                    Type = SecurityEventType.TemporaryLockoutTriggered,
                    Severity = SecurityEventSeverity.Warning,
                    Description = $"Temporary lockout after {user.Security.FailedLoginAttempts} failed attempts."
                }, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthenticationResponse>(UserErrors.InvalidCredentials);
        }

        // The caller has proved they hold the password; specific refusals are safe now.
        var canAuthenticate = user.EnsureCanAuthenticate(now);
        if (canAuthenticate.IsFailure)
        {
            await _securityEvents.WriteAsync(new SecurityEventDraft
            {
                UserId = user.Id,
                Type = SecurityEventType.LoginBlocked,
                Severity = SecurityEventSeverity.Notice,
                Description = canAuthenticate.Error.Code,
                Metadata = new Dictionary<string, string> { ["accessState"] = user.AccessState.Name }
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthenticationResponse>(canAuthenticate.Error);
        }

        if (verification == PasswordVerificationOutcome.SucceededNeedsRehash)
        {
            // The plaintext is in hand exactly once per sign-in; this is the only moment
            // an older hash can be upgraded without asking the member to do anything.
            user.UpgradePasswordHash(_passwordHasher.Hash(request.Password));
            _logger.LogInformation("Upgraded the stored password hash for user {UserId}.", user.Id);
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
            Type = SecurityEventType.LoginSucceeded,
            Severity = SecurityEventSeverity.Informational,
            Description = $"Signed in from {outcome.Session.DeviceName}."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TokenMapper.ToAuthenticationResponse(outcome, user));
    }

    private Task RecordFailureAsync(
        SharedKernel.Identity.UserId? userId,
        string description,
        CancellationToken cancellationToken) =>
        _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = userId,
            Type = SecurityEventType.LoginFailed,
            Severity = SecurityEventSeverity.Warning,
            Description = description
        }, cancellationToken);
}
