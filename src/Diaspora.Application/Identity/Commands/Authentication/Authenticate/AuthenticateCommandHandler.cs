using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Authentication;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Validation;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Security;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Authentication.Authenticate;

/// <summary>
/// The shared sign-in tail: prove identity through a provider, then run the checks and
/// token issuance every method has in common.
/// <para>
/// The division of labour is the point. The provider answers "who is this?"; everything
/// after it — the access-state gate, the device resolution, the session, the token pair,
/// the security event — is identical no matter how identity was proven. That is what makes
/// the methods interchangeable rather than three parallel copies of a sign-in.
/// </para>
/// </summary>
public sealed class AuthenticateCommandHandler : ICommandHandler<AuthenticateCommand, AuthenticationResponse>
{
    private readonly IAuthenticationProviderRegistry _providers;
    private readonly ITokenIssuanceService _tokenIssuance;
    private readonly IDeviceRegistrationService _deviceRegistration;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IRequestContext _requestContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AuthenticateCommandHandler(
        IAuthenticationProviderRegistry providers,
        ITokenIssuanceService tokenIssuance,
        IDeviceRegistrationService deviceRegistration,
        ISecurityEventWriter securityEvents,
        IRequestContext requestContext,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _providers = providers;
        _tokenIssuance = tokenIssuance;
        _deviceRegistration = deviceRegistration;
        _securityEvents = securityEvents;
        _requestContext = requestContext;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AuthenticationResponse>> Handle(
        AuthenticateCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = _providers.Resolve(new AuthenticationMethod(request.Method));
        if (provider.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(provider.Error);
        }

        var authentication = await provider.Value.AuthenticateAsync(
            new AuthenticationCredentials(request.PhoneNumber)
            {
                Password = request.Password,
                Code = request.Code
            },
            cancellationToken);

        if (authentication.IsFailure)
        {
            await RecordFailureAsync(request.Method, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthenticationResponse>(authentication.Error);
        }

        var user = authentication.Value.User;
        var now = _clock.UtcNow;

        // Identity is proven; now the state gate. A verified credential is not a licence to
        // sign in if the account is suspended or locked — the check is the same one the
        // per-request token validator runs, applied once at issuance.
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

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            SessionId = outcome.Session.Id,
            Type = SecurityEventType.LoginSucceeded,
            Severity = SecurityEventSeverity.Informational,
            Description = $"Signed in via {authentication.Value.Method} from {outcome.Session.DeviceName}."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TokenMapper.ToAuthenticationResponse(outcome, user));
    }

    private Task RecordFailureAsync(string method, CancellationToken cancellationToken) =>
        _securityEvents.WriteAsync(new SecurityEventDraft
        {
            Type = SecurityEventType.LoginFailed,
            Severity = SecurityEventSeverity.Warning,
            Description = $"Failed sign-in via {method}."
        }, cancellationToken);
}
