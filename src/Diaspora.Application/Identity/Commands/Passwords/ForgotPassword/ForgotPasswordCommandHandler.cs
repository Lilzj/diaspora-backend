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
/// Always returns the same acknowledgement, whether or not the address belongs to an
/// account, and whether or not the code could be sent. This endpoint is unauthenticated
/// and takes only an email address; any variation in the response would make it a free
/// account-enumeration tool. Real failures go to the log and the security event stream,
/// where the member cannot see them but an operator can.
/// </para>
/// <para>
/// The one thing the acknowledgement does vary with is the <b>channel</b>, and that is safe
/// precisely because it is a property of the deployment rather than of the account: every
/// caller of this endpoint gets the same channel named, whether their address is registered
/// or invented.
/// </para>
/// </summary>
public sealed class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, OperationAcknowledgedResponse>
{
    /// <summary>
    /// The same sentence whether or not the address exists, so an unregistered one
    /// cannot be told apart from a registered one.
    /// </summary>
    /// <remarks>
    /// Which of these is used depends only on what transport the deployment has configured,
    /// never on the account — see the note on the class. Naming the channel matters: a
    /// member told "check your email" while the code went to their phone will sit watching an
    /// inbox nothing is coming to, and conclude recovery is broken.
    /// </remarks>
    private const string AcknowledgementByEmail =
        "If that email address belongs to a Diaspora account, we have sent a recovery code "
        + "to it.";

    private const string AcknowledgementBySms =
        "If that email address belongs to a Diaspora account, we have sent a recovery code "
        + "by SMS to the phone number on it.";

    /// <summary>
    /// Used when the deployment has no transport at all.
    /// </summary>
    /// <remarks>
    /// Promising a code that provably cannot be sent would be a lie told to every caller, and
    /// it strands the member on a code-entry screen instead of telling them to come back. It
    /// leaks nothing: the caller learns that this deployment cannot send, not whether any
    /// particular account exists.
    /// </remarks>
    private const string AcknowledgementUndeliverable =
        "Account recovery is temporarily unavailable. Please try again later, or contact "
        + "support.";

    private readonly IUserRepository _users;
    private readonly IOtpChallengeService _otpChallenges;
    private readonly IOtpDeliveryService _delivery;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IUserRepository users,
        IOtpChallengeService otpChallenges,
        IOtpDeliveryService delivery,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _users = users;
        _otpChallenges = otpChallenges;
        _delivery = delivery;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ── Identified by email, and now delivered there too ──────────────────────────
        //
        // The member types the credential they actually use — their email — because
        // that is what they sign in with. Where the code goes is the transport's
        // decision: with `Email:Resend:ApiKey` configured, `EmailOtpDeliveryService`
        // carries recovery codes, and the code lands in the mailbox the member just
        // named. Without it the SMS transport underneath still carries them, and the
        // acknowledgement below says so instead.
        //
        // Recovery is rare either way, so this is a handful of messages rather than one
        // per sign-in — which is the cost the move to email and password exists to remove.
        //
        // Both misses below acknowledge identically, so an unregistered address cannot
        // be distinguished from a registered one.
        if (!EmailAddress.TryCreate(request.Email, out var email) || email is null)
        {
            return Acknowledge();
        }

        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            _logger.LogInformation("Recovery requested for an unregistered email address.");
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

    /// <summary>
    /// The acknowledgement for whichever channel this deployment would use.
    /// </summary>
    /// <remarks>
    /// Asked of the transport rather than worked out from configuration here, because the
    /// transport is the only thing that knows what it can actually carry — and because a
    /// copy of that reasoning in this handler would be a second place to keep in step with
    /// the first.
    /// </remarks>
    private Result<OperationAcknowledgedResponse> Acknowledge() =>
        Result.Success(OperationAcknowledgedResponse.Create(
            _delivery.ChannelFor(OtpPurpose.PasswordReset) switch
            {
                OtpDeliveryChannel.Email => AcknowledgementByEmail,
                OtpDeliveryChannel.Sms => AcknowledgementBySms,
                _ => AcknowledgementUndeliverable
            }));
}
