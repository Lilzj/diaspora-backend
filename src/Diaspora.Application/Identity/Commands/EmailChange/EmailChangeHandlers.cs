using Diaspora.Application.Abstractions;
using Diaspora.Application.Exceptions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Commands.EmailVerification;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Commands.EmailChange;

/// <summary>
/// Asks to move the signed-in member's sign-in address. Backs <c>PUT /auth/email</c>.
/// <para>
/// <b>Nothing changes here.</b> The address on the account stays the login until the signed link
/// sent to the new address is confirmed (<see cref="ConfirmEmailChangeCommand"/>).
/// </para>
/// </summary>
[Auditable(Action = "identity.user.email-change-requested")]
[RequirePermission("identity:profile:update")]
public sealed record RequestEmailChangeCommand : ICommand<EmailIdentityResponse>
{
    public required string Address { get; init; }

    public required string CurrentPassword { get; init; }
}

/// <summary>
/// Confirms an email change link. Anonymous on purpose, as verification is: the signed token is the
/// credential, and it was delivered only to the new mailbox.
/// </summary>
[Auditable(Action = "identity.user.email-changed")]
[AllowAnonymous(
    "Confirmed from a link in an email, which is the proof. The address being confirmed is the " +
    "thing in question, so a session cannot be a precondition for confirming it.")]
public sealed record ConfirmEmailChangeCommand : ICommand<EmailChangeOutcomeResponse>
{
    public required string Token { get; init; }
}

/// <summary>The outcomes a change link can have. Strings on the wire, as verification's are.</summary>
public static class EmailChangeOutcomes
{
    public const string Changed = "Changed";
    public const string AlreadyChanged = "AlreadyChanged";
    public const string AddressTaken = "AddressTaken";
    public const string Invalid = "Invalid";
}

/// <summary>The request rules.</summary>
internal static class EmailChangeRules
{
    /// <summary>
    /// Password attempts per member. The route checks a password, so it is a way to guess one from
    /// a stolen session; this bounds it the way sign-in is bounded. Spent before the password is
    /// read, so a wrong guess costs the same as a right one.
    /// </summary>
    public const int AttemptLimit = 5;

    public static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);

    public static string AttemptKey(Guid userId) => $"email-change-attempts:{userId:N}";
}

/// <summary>
/// Hides most of an address for a notice sent somewhere else: <c>joshua@gmail.com</c> becomes
/// <c>j*****@g****.com</c>. Enough for the owner to recognise their own address or know it is not
/// theirs; not enough to hand a stranger who reads the old mailbox the new one.
/// </summary>
public static class EmailAddressMask
{
    public static string Mask(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        var at = address.LastIndexOf('@');
        if (at <= 0 || at == address.Length - 1)
        {
            return "***";
        }

        var local = address[..at];
        var domain = address[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        var host = dot > 0 ? domain[..dot] : domain;
        var suffix = dot > 0 ? domain[dot..] : string.Empty;

        return $"{Hide(local)}@{Hide(host)}{suffix}";
    }

    private static string Hide(string part) =>
        part.Length <= 1 ? "*" : part[0] + new string('*', Math.Min(part.Length - 1, 8));
}

/// <summary>
/// Sends the link that will change the address, and tells the old address it was asked for.
/// <para>
/// <b>Not an enumeration oracle.</b> Whether another account already signs in with the new address
/// changes what is emailed to that address and nothing about the answer: same status, same body,
/// same rate-limit spend, same token minted. The mailbox's owner gets "someone tried to use this
/// address" instead of a link, so the one person who learns the address is registered is the
/// person it belongs to — the rule the OTP fix set for sign-in codes.
/// </para>
/// </summary>
public sealed class RequestEmailChangeCommandHandler
    : ICommandHandler<RequestEmailChangeCommand, EmailIdentityResponse>
{
    private readonly IUserRepository _users;
    private readonly ICurrentIdentityContext _current;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailChangeLinks _links;
    private readonly IEmailChangeMailer _mailer;
    private readonly ISubjectRateLimiter _limiter;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<RequestEmailChangeCommandHandler> _logger;

    public RequestEmailChangeCommandHandler(
        IUserRepository users,
        ICurrentIdentityContext current,
        IPasswordHasher passwordHasher,
        IEmailChangeLinks links,
        IEmailChangeMailer mailer,
        ISubjectRateLimiter limiter,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<RequestEmailChangeCommandHandler> logger)
    {
        _users = users;
        _current = current;
        _passwordHasher = passwordHasher;
        _links = links;
        _mailer = mailer;
        _limiter = limiter;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<EmailIdentityResponse>> Handle(
        RequestEmailChangeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_current.UserId is not { } userId)
        {
            return Result.Failure<EmailIdentityResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<EmailIdentityResponse>(UserErrors.NotFound);
        }

        var attempts = await _limiter.AcquireAsync(
            EmailChangeRules.AttemptKey(userId.Value), EmailChangeRules.AttemptLimit, EmailChangeRules.AttemptWindow,
            cancellationToken);
        if (!attempts.IsAllowed)
        {
            var wait = Math.Max(1, (int)Math.Ceiling(attempts.RetryAfter.TotalSeconds));
            throw new RateLimitException(wait, "Too many attempts. Please wait before trying again.");
        }

        if (_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordVerificationOutcome.Failed)
        {
            await _securityEvents.WriteAsync(new SecurityEventDraft
            {
                UserId = user.Id,
                SessionId = _current.SessionId,
                Type = SecurityEventType.LoginFailed,
                Severity = SecurityEventSeverity.Warning,
                Description = "Email change attempted with an incorrect current password."
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<EmailIdentityResponse>(UserErrors.IncorrectCurrentPassword);
        }

        if (!EmailAddress.TryCreate(request.Address, out var newAddress) || newAddress is null)
        {
            return Result.Failure<EmailIdentityResponse>(Error.Validation(
                nameof(request.Address), "Enter a valid email address."));
        }

        if (user.Email == newAddress)
        {
            return Result.Failure<EmailIdentityResponse>(UserErrors.EmailChangeSameAddress);
        }

        // Before the limiter, so a deployment with no email transport spends nobody's allowance.
        if (!_mailer.IsConfigured)
        {
            return Result.Failure<EmailIdentityResponse>(Error.ServiceUnavailable(
                "Identity.Email.NotConfigured", "Email delivery is not available right now."));
        }

        // The verification route's own budget, shared on purpose: both send mail on the member's
        // say-so, and one a minute and six a day is the ceiling for the pair, not for each.
        var cooldown = await _limiter.AcquireAsync(
            EmailVerificationRules.CooldownKey(userId.Value), 1, EmailVerificationRules.Cooldown, cancellationToken);
        if (!cooldown.IsAllowed)
        {
            var wait = Math.Max(1, (int)Math.Ceiling(cooldown.RetryAfter.TotalSeconds));
            throw new RateLimitException(wait, $"Please wait {wait} seconds before asking for another link.");
        }

        var daily = await _limiter.AcquireAsync(
            EmailVerificationRules.DailyKey(userId.Value), EmailVerificationRules.DailyLimit, TimeSpan.FromDays(1),
            cancellationToken);
        if (!daily.IsAllowed)
        {
            throw new RateLimitException(
                Math.Max(1, (int)Math.Ceiling(daily.RetryAfter.TotalSeconds)),
                "Too many emails today. Please try again tomorrow.");
        }

        var oldAddress = user.Email?.Value;

        // Minted whether or not it is sent, so both branches below do the same work up to the send.
        var (token, expiresAt) = _links.Create(new EmailChangeClaim(
            user.Id, oldAddress ?? string.Empty, newAddress.Value, user.Security.SecurityStamp, _current.SessionId));

        var taken = await _users.ExistsByEmailAsync(newAddress, cancellationToken);
        var language = user.PreferredLanguage.Value;

        var accepted = taken
            ? await _mailer.SendAddressUnavailableAsync(newAddress.Value, language, cancellationToken)
            : await _mailer.SendConfirmationAsync(new EmailChangeLinkMessage
            {
                Address = newAddress.Value,
                Token = token,
                ExpiresAt = expiresAt,
                Language = language
            }, cancellationToken);

        if (!accepted)
        {
            return Result.Failure<EmailIdentityResponse>(Error.ServiceUnavailable(
                "Identity.Email.SendFailed", "We could not send the email just now. Please try again in a minute."));
        }

        var masked = EmailAddressMask.Mask(newAddress.Value);

        if (oldAddress is not null)
        {
            // Best effort. The completion notice goes to the same address, so a lost request
            // notice is not the member's only warning.
            var noticed = await _mailer.SendNoticeAsync(new EmailChangeNotice
            {
                Address = oldAddress,
                MaskedNewAddress = masked,
                Stage = EmailChangeNoticeStage.Requested,
                LinkExpiresAt = expiresAt,
                Language = language
            }, cancellationToken);

            if (!noticed)
            {
                _logger.LogWarning("The email-change notice to the current address of {UserId} was not accepted.", userId.Value);
            }
        }

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            SessionId = _current.SessionId,
            Type = SecurityEventType.EmailChangeRequested,
            Severity = SecurityEventSeverity.Notice,
            Description = $"Email change to {masked} requested; nothing changes until it is confirmed."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new EmailIdentityResponse
        {
            Address = oldAddress ?? string.Empty,
            IsVerified = user.IsEmailVerified,
            VerificationSentAt = _clock.UtcNow,
            VerificationExpiresAt = expiresAt,
            CooldownSeconds = (int)EmailVerificationRules.Cooldown.TotalSeconds,
            DeliveryState = EmailVerificationRules.Ok,
            PendingAddress = newAddress.Value
        });
    }
}

/// <summary>
/// Makes the change a link asked for, once, and only while nothing has happened since that should
/// stop it.
/// <para>
/// One public answer, <c>Invalid</c>, for a forged, expired, superseded or cancelled link, as on
/// the verification page. <c>AddressTaken</c> is told only to whoever holds the link, which was
/// delivered only to that address.
/// </para>
/// </summary>
public sealed class ConfirmEmailChangeCommandHandler
    : ICommandHandler<ConfirmEmailChangeCommand, EmailChangeOutcomeResponse>
{
    private readonly IUserRepository _users;
    private readonly IEmailChangeLinks _links;
    private readonly IEmailChangeMailer _mailer;
    private readonly ISessionRevocationService _revocation;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ConfirmEmailChangeCommandHandler> _logger;

    public ConfirmEmailChangeCommandHandler(
        IUserRepository users,
        IEmailChangeLinks links,
        IEmailChangeMailer mailer,
        ISessionRevocationService revocation,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ConfirmEmailChangeCommandHandler> logger)
    {
        _users = users;
        _links = links;
        _mailer = mailer;
        _revocation = revocation;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<EmailChangeOutcomeResponse>> Handle(
        ConfirmEmailChangeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var claim = _links.Redeem(request.Token);
        if (claim is null || !EmailAddress.TryCreate(claim.ToAddress, out var newAddress) || newAddress is null)
        {
            return Outcome(EmailChangeOutcomes.Invalid);
        }

        var user = await _users.GetByIdAsync(claim.UserId, cancellationToken);
        if (user is null)
        {
            return Outcome(EmailChangeOutcomes.Invalid);
        }

        if (user.Email == newAddress)
        {
            return Outcome(EmailChangeOutcomes.AlreadyChanged);
        }

        var now = _clock.UtcNow;

        // The address it moves from must still be the one on the account (else another change
        // completed since, and this link is spent); the stamp must be unrotated (else the password
        // changed since, which is how the old address's owner cancels); and the account must still
        // be one that may sign in.
        if (!string.Equals(user.Email?.Value ?? string.Empty, claim.FromAddress, StringComparison.Ordinal)
            || user.Security.SecurityStamp != claim.SecurityStamp
            || user.EnsureCanAuthenticate(now).IsFailure)
        {
            return Outcome(EmailChangeOutcomes.Invalid);
        }

        if (await _users.ExistsByEmailAsync(newAddress, cancellationToken))
        {
            return Outcome(EmailChangeOutcomes.AddressTaken);
        }

        var oldAddress = user.Email?.Value;

        var changed = user.ChangeEmail(newAddress, now);
        if (changed.IsFailure)
        {
            return Outcome(EmailChangeOutcomes.AlreadyChanged);
        }

        _users.Update(user);

        // Every session but the one that asked: a member who changes their address on their phone
        // stays signed in there, and anybody else who was in the account is not.
        var revoked = claim.RequestedBySession is { } kept
            ? await _revocation.RevokeAllExceptAsync(user.Id, kept, SessionRevocationReason.EmailChanged, now, cancellationToken)
            : await _revocation.RevokeAllAsync(user.Id, SessionRevocationReason.EmailChanged, now, cancellationToken);

        var masked = EmailAddressMask.Mask(newAddress.Value);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            SessionId = claim.RequestedBySession,
            Type = SecurityEventType.EmailChanged,
            Severity = SecurityEventSeverity.Notice,
            Description = $"Email changed to {masked}; {revoked} other session(s) revoked."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (oldAddress is not null)
        {
            var noticed = await _mailer.SendNoticeAsync(new EmailChangeNotice
            {
                Address = oldAddress,
                MaskedNewAddress = masked,
                Stage = EmailChangeNoticeStage.Completed,
                Language = user.PreferredLanguage.Value
            }, cancellationToken);

            if (!noticed)
            {
                _logger.LogWarning("The email-changed notice to the previous address of {UserId} was not accepted.", user.Id.Value);
            }
        }

        return Outcome(EmailChangeOutcomes.Changed);
    }

    private static Result<EmailChangeOutcomeResponse> Outcome(string outcome) =>
        Result.Success(new EmailChangeOutcomeResponse { Outcome = outcome });
}
