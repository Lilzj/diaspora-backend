using Diaspora.Application.Abstractions;
using Diaspora.Application.Exceptions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Application.Identity.Commands.EmailVerification;

/// <summary>The signed-in member's address and its verification state. Backs <c>GET /auth/email</c>.</summary>
[RequirePermission("identity:profile:read")]
public sealed record GetEmailIdentityQuery : IQuery<EmailIdentityResponse>;

/// <summary>Sends a verification link to the address on file. Backs <c>POST /auth/email/verification</c>.</summary>
[Auditable(Action = "identity.user.email-verification-sent")]
[RequirePermission("identity:profile:update")]
public sealed record SendEmailVerificationCommand : ICommand<EmailIdentityResponse>;

/// <summary>
/// Confirms a verification link. Anonymous on purpose: the signed token is the credential, and a
/// member often opens the email on a device where they are not signed in.
/// </summary>
[Auditable(Action = "identity.user.email-verified")]
[AllowAnonymous(
    "Confirmed from a link in an email, which is the proof. The address being confirmed is the " +
    "thing in question, so a session cannot be a precondition for confirming it.")]
public sealed record ConfirmEmailVerificationCommand : ICommand<EmailVerificationOutcomeResponse>
{
    public required string Token { get; init; }
}

/// <summary>The resend rules, in one place so the read and the send agree on the cooldown.</summary>
internal static class EmailVerificationRules
{
    /// <summary>Between two links. Shown to the member as a countdown.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Links per member per day. Resend is member-initiated only, and this keeps the route from
    /// becoming a way to flood whatever address is on an account with mail.
    /// </summary>
    public const int DailyLimit = 6;

    // Each key's purpose is the text before its first colon, and the platform limiter keeps one
    // policy per purpose. These once shared "email-verification:", so every send rebuilt one policy
    // alternately as one-a-minute and six-a-day (found 5 Oct 2026, from a "10 seconds" countdown).
    public static string CooldownKey(Guid userId) => $"email-verification-cooldown:{userId:N}";

    public static string DailyKey(Guid userId) => $"email-verification-daily:{userId:N}";

    public const string Ok = "ok";
}

public sealed class GetEmailIdentityQueryHandler : IQueryHandler<GetEmailIdentityQuery, EmailIdentityResponse>
{
    private readonly IUserRepository _users;
    private readonly ICurrentIdentityContext _current;

    public GetEmailIdentityQueryHandler(IUserRepository users, ICurrentIdentityContext current)
    {
        _users = users;
        _current = current;
    }

    public async Task<Result<EmailIdentityResponse>> Handle(
        GetEmailIdentityQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not { } userId)
        {
            return Result.Failure<EmailIdentityResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<EmailIdentityResponse>(UserErrors.NotFound);
        }

        // The cooldown is not read from the limiter here: asking it would spend a permit. A plain
        // read reports zero, and the send route answers with the real figure when it refuses.
        return Result.Success(new EmailIdentityResponse
        {
            Address = user.Email?.Value ?? string.Empty,
            IsVerified = user.IsEmailVerified,
            CooldownSeconds = 0,
            DeliveryState = EmailVerificationRules.Ok
        });
    }
}

public sealed class SendEmailVerificationCommandHandler
    : ICommandHandler<SendEmailVerificationCommand, EmailIdentityResponse>
{
    private readonly IUserRepository _users;
    private readonly ICurrentIdentityContext _current;
    private readonly IEmailVerificationLinks _links;
    private readonly IEmailVerificationSender _sender;
    private readonly ISubjectRateLimiter _limiter;
    private readonly IClock _clock;

    public SendEmailVerificationCommandHandler(
        IUserRepository users,
        ICurrentIdentityContext current,
        IEmailVerificationLinks links,
        IEmailVerificationSender sender,
        ISubjectRateLimiter limiter,
        IClock clock)
    {
        _users = users;
        _current = current;
        _links = links;
        _sender = sender;
        _limiter = limiter;
        _clock = clock;
    }

    public async Task<Result<EmailIdentityResponse>> Handle(
        SendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not { } userId)
        {
            return Result.Failure<EmailIdentityResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<EmailIdentityResponse>(UserErrors.NotFound);
        }

        if (user.Email is null)
        {
            return Result.Failure<EmailIdentityResponse>(UserErrors.NoEmailOnAccount);
        }

        if (user.IsEmailVerified)
        {
            return Result.Failure<EmailIdentityResponse>(UserErrors.EmailAlreadyVerified);
        }

        // Checked before the limiter, so a deployment with no email transport does not spend a
        // member's daily allowance on sends that could never have left.
        if (!_sender.IsConfigured)
        {
            return Result.Failure<EmailIdentityResponse>(Error.ServiceUnavailable(
                "Identity.Email.NotConfigured", "Email delivery is not available right now."));
        }

        var cooldown = await _limiter.AcquireAsync(
            EmailVerificationRules.CooldownKey(userId.Value), 1, EmailVerificationRules.Cooldown, cancellationToken);
        if (!cooldown.IsAllowed)
        {
            // Thrown rather than returned: the exception middleware turns it into a 429 with a
            // Retry-After header, which the app reads into a countdown. A returned RateLimited error
            // carries the wait only in its prose, and the app replaced it with "please wait"
            // (seen on the device, 5 Oct 2026).
            var wait = Math.Max(1, (int)Math.Ceiling(cooldown.RetryAfter.TotalSeconds));
            throw new RateLimitException(wait, $"Please wait {wait} seconds before asking for another link.");
        }

        var daily = await _limiter.AcquireAsync(
            EmailVerificationRules.DailyKey(userId.Value), EmailVerificationRules.DailyLimit, TimeSpan.FromDays(1), cancellationToken);
        if (!daily.IsAllowed)
        {
            throw new RateLimitException(
                Math.Max(1, (int)Math.Ceiling(daily.RetryAfter.TotalSeconds)),
                "Too many verification emails today. Please try again tomorrow.");
        }

        var (token, expiresAt) = _links.Create(user.Id, user.Email.Value);

        var accepted = await _sender.SendAsync(new EmailVerificationMessage
        {
            Address = user.Email.Value,
            Token = token,
            ExpiresAt = expiresAt,
            Language = user.PreferredLanguage.Value
        }, cancellationToken);

        if (!accepted)
        {
            // Unanswered is not no: the provider may have taken it. The member is told it did not
            // go, and may try again after the cooldown, which is the cheaper mistake.
            return Result.Failure<EmailIdentityResponse>(Error.ServiceUnavailable(
                "Identity.Email.SendFailed", "We could not send the email just now. Please try again in a minute."));
        }

        return Result.Success(new EmailIdentityResponse
        {
            Address = user.Email.Value,
            IsVerified = false,
            VerificationSentAt = _clock.UtcNow,
            VerificationExpiresAt = expiresAt,
            CooldownSeconds = (int)EmailVerificationRules.Cooldown.TotalSeconds,
            DeliveryState = EmailVerificationRules.Ok
        });
    }
}

public sealed class ConfirmEmailVerificationCommandHandler
    : ICommandHandler<ConfirmEmailVerificationCommand, EmailVerificationOutcomeResponse>
{
    private readonly IUserRepository _users;
    private readonly IEmailVerificationLinks _links;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ConfirmEmailVerificationCommandHandler(
        IUserRepository users, IEmailVerificationLinks links, IUnitOfWork unitOfWork, IClock clock)
    {
        _users = users;
        _links = links;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<EmailVerificationOutcomeResponse>> Handle(
        ConfirmEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // One answer for forged, malformed, expired and unknown-member links. The page is public, and
        // distinguishing them would tell a stranger which member ids exist.
        var claim = _links.Redeem(request.Token);
        if (claim is null || !EmailAddress.TryCreate(claim.Address, out var address) || address is null)
        {
            return Outcome("Invalid");
        }

        var user = await _users.GetByIdAsync(claim.UserId, cancellationToken);
        if (user is null)
        {
            return Outcome("Invalid");
        }

        if (user.IsEmailVerified && user.Email == address)
        {
            return Outcome("AlreadyVerified");
        }

        var confirmed = user.ConfirmEmailVerified(address, _clock.UtcNow);
        if (confirmed.IsFailure)
        {
            return Outcome("AddressChanged");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Outcome("Verified");
    }

    private static Result<EmailVerificationOutcomeResponse> Outcome(string outcome) =>
        Result.Success(new EmailVerificationOutcomeResponse { Outcome = outcome });
}
