using Diaspora.Application.Identity.Commands.EmailVerification;
using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Api.RateLimiting;
using Diaspora.Platform.Http;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Commands.Authentication.Authenticate;
using Diaspora.Application.Identity.Commands.Authentication.Login;
using Diaspora.Application.Identity.Commands.Authentication.Logout;
using Diaspora.Application.Identity.Commands.Authentication.RefreshToken;
using Diaspora.Application.Identity.Commands.Authentication.RequestSignInOtp;
using Diaspora.Application.Identity.Commands.Passwords.ChangePassword;
using Diaspora.Application.Identity.Commands.Passwords.ForgotPassword;
using Diaspora.Application.Identity.Commands.Passwords.ResetPassword;
using Diaspora.Application.Identity.Commands.PhoneVerification.ResendOtp;
using Diaspora.Application.Identity.Commands.PhoneVerification.VerifyPhone;
using Diaspora.Application.Identity.Commands.Registration;
using Diaspora.Application.Identity.Queries.GetCurrentUser;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Diaspora.Api.Identity.Controllers;

/// <summary>
/// Registration, authentication, phone verification and password management.
/// <para>
/// Every action is a thin translation from DTO to command. No routing logic lives here —
/// where an authenticated member should be sent is the Access Gateway's decision, and it
/// travels back inside the response rather than being re-derived per endpoint.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/auth")]
[Produces("application/json")]
public sealed class AuthController : ApiController
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Creates a Diaspora account and sends the first verification code.</summary>
    /// <remarks>
    /// Returns no tokens. The account holds no usable access until the phone is verified.
    /// </remarks>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.RegistrationPolicy)]
    [ProducesResponseType<ApiResponse<RegistrationResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new RegisterUserCommand
        {
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Password = request.Password,
            Nationality = request.Nationality,
            CountryOfResidence = request.CountryOfResidence,
            PreferredLanguage = request.PreferredLanguage,
            PreferredTimeZone = request.PreferredTimeZone
        }, cancellationToken);

        return HandleCreated(result);
    }

    /// <summary>Redeems the phone verification code and issues the first token pair.</summary>
    [HttpPost("verify-phone")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.SignInPolicy)]
    [ProducesResponseType<ApiResponse<AuthenticationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyPhoneAsync(
        [FromBody] VerifyPhoneRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new VerifyPhoneCommand
        {
            PhoneNumber = request.PhoneNumber,
            Code = request.Code,
            DeviceIdentifier = request.DeviceIdentifier,
            DeviceName = request.DeviceName,
            Platform = request.Platform,
            AppVersion = request.AppVersion
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Requests a replacement one-time code.</summary>
    [HttpPost("resend-otp")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.OneTimeCodePolicy)]
    [ProducesResponseType<ApiResponse<OtpChallengeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendOtpAsync(
        [FromBody] ResendOtpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new ResendOtpCommand
        {
            PhoneNumber = request.PhoneNumber,
            Purpose = request.Purpose
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Exchanges credentials for a session and token pair.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.SignInPolicy)]
    [ProducesResponseType<ApiResponse<AuthenticationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new LoginCommand
        {
            Email = request.Email,
            Password = request.Password,
            DeviceIdentifier = request.DeviceIdentifier,
            DeviceName = request.DeviceName,
            Platform = request.Platform,
            AppVersion = request.AppVersion
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Starts a passwordless sign-in by texting a one-time code.</summary>
    /// <remarks>
    /// The frontend's primary sign-in. Anonymous, and the response is identical whether or
    /// not the number is registered, so it cannot enumerate accounts. Redeem the code at
    /// <c>POST /auth/otp/sign-in</c>.
    /// </remarks>
    [HttpPost("otp/start")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.OneTimeCodePolicy)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> StartOtpSignInAsync(
        [FromBody] RequestSignInOtpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new RequestSignInOtpCommand
        {
            PhoneNumber = request.PhoneNumber
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Completes a passwordless sign-in by redeeming the one-time code.</summary>
    /// <remarks>
    /// Runs through the authentication-provider registry with the <c>phone-otp</c> method,
    /// then the same access-state gate and token issuance every sign-in shares — so the
    /// response is identical to <c>POST /auth/login</c>.
    /// </remarks>
    [HttpPost("otp/sign-in")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.SignInPolicy)]
    [ProducesResponseType<ApiResponse<AuthenticationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> OtpSignInAsync(
        [FromBody] OtpSignInRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new AuthenticateCommand
        {
            Method = "phone-otp",
            PhoneNumber = request.PhoneNumber,
            Code = request.Code,
            DeviceIdentifier = request.DeviceIdentifier,
            DeviceName = request.DeviceName,
            Platform = request.Platform,
            AppVersion = request.AppVersion
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Rotates a refresh token for a new token pair.</summary>
    /// <remarks>
    /// Anonymous by design: the refresh token is the credential, and the access token it
    /// renews has usually expired by the time this is called.
    /// </remarks>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.RefreshPolicy)]
    [ProducesResponseType<ApiResponse<AuthenticationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshAsync(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new RefreshTokenCommand
        {
            RefreshToken = request.RefreshToken
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Ends the caller's session, optionally every other session too.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAsync(
        [FromBody] LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new LogoutCommand
        {
            RevokeAllSessions = request?.RevokeAllSessions ?? false
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Starts account recovery.</summary>
    /// <remarks>
    /// Always returns the same acknowledgement, so the endpoint cannot be used to discover
    /// which phone numbers are registered.
    /// </remarks>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.RecoveryPolicy)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new ForgotPasswordCommand
        {
            Email = request.Email
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Completes account recovery and revokes every session.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.RecoveryPolicy)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new ResetPasswordCommand
        {
            Email = request.Email,
            Code = request.Code,
            NewPassword = request.NewPassword
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Changes the password and revokes every other session.</summary>
    [HttpPost("change-password")]
    [Authorize(Policy = DiasporaPolicies.ChangeOwnPassword)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new ChangePasswordCommand
        {
            CurrentPassword = request.CurrentPassword,
            NewPassword = request.NewPassword
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>The member's email address and whether they have proven they receive mail there.</summary>
    [HttpGet("email")]
    [Authorize(Policy = DiasporaPolicies.ReadOwnProfile)]
    [ProducesResponseType<ApiResponse<EmailIdentityResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEmailAsync(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetEmailIdentityQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Sends a verification link to the address on file. At most one a minute and six a day; a
    /// refusal is a 429 whose message says how long to wait.
    /// </summary>
    [HttpPost("email/verification")]
    [Authorize(Policy = DiasporaPolicies.UpdateOwnProfile)]
    [ProducesResponseType<ApiResponse<EmailIdentityResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendEmailVerificationAsync(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SendEmailVerificationCommand(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Confirms a verification link from inside the app, when an App Link opened the app instead of
    /// the browser. Anonymous: the signed token is the credential, as on the web page.
    /// </summary>
    [HttpPost("email/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.RecoveryPolicy)]
    [ProducesResponseType<ApiResponse<EmailVerificationOutcomeResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmEmailAsync(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(
            new ConfirmEmailVerificationCommand { Token = request.Token ?? string.Empty }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Returns the signed-in member's own profile.</summary>
    [HttpGet("me")]
    [Authorize(Policy = DiasporaPolicies.ReadOwnProfile)]
    [ProducesResponseType<ApiResponse<UserProfileResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCurrentUserQuery(), cancellationToken);
        return HandleResult(result);
    }
}
