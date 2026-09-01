using System.Globalization;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Validation;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Email;

/// <summary>
/// Carries recovery codes by email, and everything else by whatever was registered before it.
/// </summary>
/// <remarks>
/// <para>
/// A decorator rather than a replacement, because the two transports are not alternatives: a
/// phone-verification code has to reach the phone being verified, and no email can do that. So
/// this wraps the SMS service instead of standing in for it, and forwards every purpose it should
/// not carry.
/// </para>
/// <para>
/// <b>Why this exists.</b> Sign-in moved from an SMS code to email and password to stop paying for
/// a message on every login. Recovery was the hole left in that: it identifies the member by email
/// and then sent the code by SMS, because at the time nothing could send an email. That made the
/// one flow least able to tolerate a delivery failure depend on the channel the change existed to
/// stop using — and on a registered phone that an email-first signup need not have verified.
/// </para>
/// </remarks>
public sealed class EmailOtpDeliveryService : IOtpDeliveryService
{
    private readonly IOtpDeliveryService _inner;
    private readonly IEmailGateway _gateway;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly IResilienceExecutor _resilience;
    private readonly ILocalizedMessageProvider _messages;
    private readonly IClock _clock;
    private readonly ILogger<EmailOtpDeliveryService> _logger;

    public EmailOtpDeliveryService(
        IOtpDeliveryService inner,
        IEmailGateway gateway,
        IOptionsMonitor<EmailOptions> options,
        IResilienceExecutor resilience,
        ILocalizedMessageProvider messages,
        IClock clock,
        ILogger<EmailOtpDeliveryService> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resilience);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(clock);

        _inner = inner;
        _gateway = gateway;
        _options = options;
        _resilience = resilience;
        _messages = messages;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        OtpDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!CanCarry(request.Purpose)
            || string.IsNullOrWhiteSpace(request.EmailAddress)
            || !IsConfigured)
        {
            return await _inner.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var outbound = Compose(request, request.EmailAddress);
        var dependency = EmailDependencies.For(_gateway.Provider);

        try
        {
            var dispatch = await _resilience.ExecuteAsync(
                dependency,
                token => _gateway.SendAsync(outbound, token),
                cancellationToken).ConfigureAwait(false);

            // Never the code, and never the address. This line exists to be matched against a
            // support conversation, not to reconstruct one.
            _logger.LogInformation(
                "A {Purpose} code was accepted by {Provider} ({MessageId}).",
                request.Purpose, dispatch.Provider, dispatch.ProviderMessageId ?? "no id");

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host is shutting down or the caller gave up. Not a delivery failure, and
            // falling through to SMS here would send a message nobody is waiting for.
            throw;
        }
        catch (Exception exception) when (exception is EmailPermanentException
                                              or EmailTransientException
                                              or CircuitOpenException)
        {
            // ── Falling back costs an SMS, and is still right ─────────────────────────────
            //
            // This is the one place that reintroduces the per-message cost the email migration
            // removed, so it is worth being explicit about the trade. It fires only when email
            // delivery has actually failed, on a flow a member reaches a handful of times in the
            // life of an account. A member locked out because their code went nowhere is a far
            // more expensive outcome than a message.
            //
            // The member was told to expect an email — `ChannelFor` said so — so this is logged
            // at warning rather than swallowed: a deployment where it fires routinely has an
            // email problem to fix, not a fallback to rely on.
            _logger.LogWarning(
                exception,
                "{Provider} did not carry a {Purpose} code; falling back to the SMS transport.",
                _gateway.Provider, request.Purpose);

            return await _inner.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Email for the purposes it can serve, and the wrapped transport's answer for the rest.
    /// </summary>
    public OtpDeliveryChannel ChannelFor(OtpPurpose purpose) =>
        CanCarry(purpose) && IsConfigured
            ? OtpDeliveryChannel.Email
            : _inner.ChannelFor(purpose);

    private bool IsConfigured =>
        _gateway.IsConfigured && _options.CurrentValue.From.IsConfigured;

    /// <summary>
    /// Which purposes email is allowed to carry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="OtpPurpose.PhoneVerification"/> is excluded because the code <em>is</em> the
    /// proof: it has to arrive on the number being claimed, and an emailed one would verify
    /// nothing.
    /// </para>
    /// <para>
    /// <see cref="OtpPurpose.StepUpAuthentication"/> and <see cref="OtpPurpose.SignIn"/> are
    /// excluded for a different reason. Both are second factors behind a password, and an emailed
    /// code is not a second factor when the mailbox is where a password reset would also land —
    /// whoever holds the mailbox holds both. SMS is a genuinely separate channel; email is not.
    /// </para>
    /// <para>
    /// What is left is recovery, where the member has already lost the password and the mailbox
    /// is the credential they still hold.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The category a recovery code is filed under, for choosing its sender.
    /// </summary>
    /// <remarks>
    /// The literal rather than a reference to <c>NotificationCategoryKeys</c>: a one-time code is
    /// not a notification and does not travel through that module, so borrowing its constant would
    /// be a dependency this path does not otherwise have.
    /// </remarks>
    private const string SecurityCategoryKey = "security";

    private static bool CanCarry(OtpPurpose purpose) =>
        purpose is OtpPurpose.PasswordReset or OtpPurpose.AccountRecovery;

    private OutboundEmail Compose(OtpDeliveryRequest request, string address)
    {
        var remaining = request.ExpiresAt - _clock.UtcNow;

        // At least one, always. A code that expires in forty seconds reads as "0 minutes", which
        // looks like an already-dead code and has the member ask for another one.
        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));

        // A recovery code is security mail, so it comes from the security mailbox rather than
        // the one receipts and reminders use. A member who has just been told their password can
        // be reset should recognise the sender.
        var identity = _options.CurrentValue.SenderFor(SecurityCategoryKey);

        var subject = _messages.GetFor(IdentityMessageKeys.OtpEmailSubject, request.Language);
        var body = Body(request, minutes);

        // The code is the whole message, so it is given its own line and its own weight rather
        // than being left to sit inside a sentence a member has to re-read on a phone.
        var footer = _messages.GetFor(IdentityMessageKeys.OtpEmailFooter, request.Language);

        return new OutboundEmail
        {
            Recipients = [address],
            Subject = subject,
            TextBody = EmailLayout.Text(subject, body, footerNote: footer),
            HtmlBody = EmailLayout.Html(subject, body, footerNote: footer),

            FromAddress = identity.Address,
            FromName = identity.Name,

            // No unsubscribe. This is a code the member asked for seconds ago, not a message a
            // preference governs — offering to unsubscribe from it would be offering to opt out
            // of signing in.
            UnsubscribeUrl = null
        };
    }

    private string Body(OtpDeliveryRequest request, int minutes)
    {
        var template = _messages.GetFor(IdentityMessageKeys.OtpEmailBody, request.Language);

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, request.Code, minutes);
        }
        catch (FormatException)
        {
            // A translation with a malformed placeholder must not cost the member their code.
            // The digits alone still work; the sentence around them is what is lost.
            _logger.LogError(
                "The OTP email template for language '{Language}' has a malformed placeholder.",
                request.Language);

            return request.Code;
        }
    }
}
