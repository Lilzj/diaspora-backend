using System.Globalization;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Email;

/// <summary>
/// Sends the email verification link through the configured gateway (Resend).
/// <para>
/// The link points at the API's own confirmation page, <c>{AppBaseUrl}/email/confirm</c>, which
/// verifies only when its button is pressed. Mail security scanners open every link in a message;
/// a link that verified on open would be verified by a robot.
/// </para>
/// </summary>
public sealed class GatewayEmailVerificationSender : IEmailVerificationSender
{
    public const string ConfirmPath = "/email/confirm";

    private const string SecurityCategoryKey = "security";

    private readonly IEmailGateway _gateway;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly IResilienceExecutor _resilience;
    private readonly ILocalizedMessageProvider _messages;
    private readonly IClock _clock;
    private readonly ILogger<GatewayEmailVerificationSender> _logger;

    public GatewayEmailVerificationSender(
        IEmailGateway gateway,
        IOptionsMonitor<EmailOptions> options,
        IResilienceExecutor resilience,
        ILocalizedMessageProvider messages,
        IClock clock,
        ILogger<GatewayEmailVerificationSender> logger)
    {
        _gateway = gateway;
        _options = options;
        _resilience = resilience;
        _messages = messages;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Needs a public base URL as well as a transport: a verification email with nowhere to go is
    /// worse than none, because the member waits for it and it cannot work.
    /// </summary>
    public bool IsConfigured =>
        _gateway.IsConfigured
        && _options.CurrentValue.From.IsConfigured
        && Uri.TryCreate(_options.CurrentValue.AppBaseUrl, UriKind.Absolute, out _);

    public async Task<bool> SendAsync(EmailVerificationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsConfigured) return false;

        var settings = _options.CurrentValue;
        var link = $"{settings.AppBaseUrl.TrimEnd('/')}{ConfirmPath}?token={Uri.EscapeDataString(message.Token)}";
        var hours = Math.Max(1, (int)Math.Floor((message.ExpiresAt - _clock.UtcNow).TotalHours));

        var subject = _messages.GetFor(IdentityMessageKeys.EmailVerificationSubject, message.Language);
        var body = string.Format(
            CultureInfo.InvariantCulture,
            _messages.GetFor(IdentityMessageKeys.EmailVerificationBody, message.Language),
            hours);
        var button = _messages.GetFor(IdentityMessageKeys.EmailVerificationButton, message.Language);
        var footer = _messages.GetFor(IdentityMessageKeys.EmailVerificationFooter, message.Language);
        var identity = settings.SenderFor(SecurityCategoryKey);

        var outbound = new OutboundEmail
        {
            Recipients = [message.Address],
            Subject = subject,
            TextBody = EmailLayout.Text(subject, body, button, link, footerNote: footer),
            HtmlBody = EmailLayout.Html(subject, body, button, link, footerNote: footer),
            FromAddress = identity.Address,
            FromName = identity.Name,
            UnsubscribeUrl = null
        };

        try
        {
            var dispatch = await _resilience.ExecuteAsync(
                EmailDependencies.For(_gateway.Provider),
                token => _gateway.SendAsync(outbound, token),
                cancellationToken).ConfigureAwait(false);

            // Never the address and never the link: the link is a credential until it expires.
            _logger.LogInformation(
                "A verification email was accepted by {Provider} ({MessageId}).",
                dispatch.Provider, dispatch.ProviderMessageId ?? "no id");

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is EmailPermanentException
                                              or EmailTransientException
                                              or CircuitOpenException)
        {
            _logger.LogWarning(exception, "{Provider} did not carry a verification email.", _gateway.Provider);
            return false;
        }
    }
}
