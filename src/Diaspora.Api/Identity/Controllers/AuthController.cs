using Diaspora.Application.Identity.Commands.EmailChange;
using Diaspora.Application.Identity.Commands.EmailVerification;
using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Api.RateLimiting;
using Diaspora.Platform.Http;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Commands.Authentication.Login;
using Diaspora.Application.Identity.Commands.Authentication.Logout;
using Diaspora.Application.Identity.Commands.Authentication.RefreshToken;
using Diaspora.Application.Identity.Commands.Passwords.ChangePassword;
using Diaspora.Application.Identity.Commands.Passwords.ForgotPassword;
using Diaspora.Application.Identity.Commands.Passwords.ResetPassword;
using Diaspora.Application.Identity.Commands.PhoneVerification.ResendOtp;
using Diaspora.Application.Identity.Commands.PhoneVerification.VerifyPhone;
using Diaspora.Application.Identity.Commands.Registration;
using Diaspora.Application.Identity.Commands.StepUp;
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
            PreferredTimeZone = request.PreferredTimeZone,
            ReferralCode = request.ReferralCode
        }, cancellationToken);

        return HandleCreated(result);
    }

    /// <summary>Whether a password would be accepted, checked by the same policy as registration.</summary>
    /// <remarks>
    /// Creates nothing and touches no account, so it says nothing about who is registered.
    /// 200 when the password would be accepted; otherwise the same 400 registration would
    /// give, with the same localised message.
    /// </remarks>
    [HttpPost("password-check")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.SignInPolicy)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult CheckPassword(
        [FromBody] CheckPasswordRequest request,
        [FromServices] IPasswordPolicy passwordPolicy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(passwordPolicy);

        return HandleResult(passwordPolicy.Validate(request.Password, new PasswordPolicyContext
        {
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber
        }));
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

    // Sign-in by one-time code (`otp/start`, `otp/sign-in`) was removed on 5 Oct 2026:
    // email and password is the only way to sign in. Codes still verify a phone number.

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

    // ─── Step-up authentication (BSEC-007) ──────────────────────────────────────────────────
    //
    // Two actions, and between them they are the only route past a step-up refusal. Both are
    // authenticated with a bare [Authorize] rather than a policy, deliberately: a member who can be
    // refused a withdrawal must always be able to ask for the code that lets them through, and a
    // policy here would create an account that is permanently stuck at the refusal.

    /// <summary>Sends a code to confirm a sensitive operation, such as a payout.</summary>
    /// <remarks>
    /// To the registered phone, never to the email — a code arriving in the mailbox that would also
    /// receive a password reset is not a second factor.
    /// <para>
    /// Throttled on the one-time-code budget rather than the authenticated one, because this sends
    /// an SMS: the cost of an unchecked request is the platform's, not the caller's.
    /// </para>
    /// </remarks>
    [HttpPost("step-up")]
    [Authorize]
    [EnableRateLimiting(RateLimitingRegistration.OneTimeCodePolicy)]
    [ProducesResponseType<ApiResponse<OtpChallengeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestStepUpAsync(
        [FromBody] RequestStepUpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new RequestStepUpCommand
        {
            Scope = request.Scope
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Redeems a step-up code. The grant it earns belongs to this session and no other.
    /// </summary>
    /// <remarks>
    /// Throttled on the sign-in budget, which is the code-guessing budget: this is a surface where
    /// the caller supplies six digits and asks whether they are right.
    /// </remarks>
    [HttpPost("step-up/verify")]
    [Authorize]
    [EnableRateLimiting(RateLimitingRegistration.SignInPolicy)]
    [ProducesResponseType<ApiResponse<StepUpGrantResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CompleteStepUpAsync(
        [FromBody] CompleteStepUpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new CompleteStepUpCommand
        {
            Scope = request.Scope,
            Code = request.Code
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

    /// <summary>
    /// Asks to change the sign-in address. Needs the current password. Nothing changes until the
    /// link sent to the new address is confirmed; until then the current address is still the login.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer is the same whether or not another account already uses the new address: the
    /// email that goes there differs, the response does not. <c>pendingAddress</c> echoes the
    /// address asked for; a later <c>GET /auth/email</c> does not remember it.
    /// </para>
    /// <para>
    /// The current address is told a change was asked for, with the new one masked. Shares the
    /// verification route's allowance of one email a minute and six a day; five password attempts
    /// per fifteen minutes. A wrong password is a 400 <c>Identity.User.IncorrectCurrentPassword</c>.
    /// </para>
    /// </remarks>
    [HttpPut("email")]
    [Authorize(Policy = DiasporaPolicies.UpdateOwnProfile)]
    [EnableRateLimiting(RateLimitingRegistration.SignInPolicy)]
    [ProducesResponseType<ApiResponse<EmailIdentityResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChangeEmailAsync(
        [FromBody] ChangeEmailRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new RequestEmailChangeCommand
        {
            Address = request.Address ?? string.Empty,
            CurrentPassword = request.CurrentPassword ?? string.Empty
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Confirms an email change link from inside the app, when an App Link opened the app instead
    /// of the browser. Anonymous: the signed token is the credential, as on the web page.
    /// </summary>
    /// <remarks>
    /// <c>outcome</c> is <c>Changed</c>, <c>AlreadyChanged</c>, <c>AddressTaken</c> or <c>Invalid</c>.
    /// A change signs out every session except the one that asked for it.
    /// </remarks>
    [HttpPost("email/change/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingRegistration.RecoveryPolicy)]
    [ProducesResponseType<ApiResponse<EmailChangeOutcomeResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmEmailChangeAsync(
        [FromBody] ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(
            new ConfirmEmailChangeCommand { Token = request.Token ?? string.Empty }, cancellationToken);

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
