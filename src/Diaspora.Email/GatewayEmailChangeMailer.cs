using System.Globalization;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Email;

/// <summary>
/// Sends the emails an address change needs through the configured gateway (Resend).
/// <para>
/// The link points at the API's own page, <c>{AppBaseUrl}/email/change</c>, which changes nothing
/// until its button is pressed — mail scanners open every link, and a change made on open would be
/// made by a robot. Every message goes out under the security sender, with no unsubscribe line:
/// these are about the account itself, not something a member can opt out of.
/// </para>
/// </summary>
public sealed class GatewayEmailChangeMailer : IEmailChangeMailer
{
    public const string ConfirmPath = "/email/change";

    private const string SecurityCategoryKey = "security";

    private readonly IEmailGateway _gateway;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly IResilienceExecutor _resilience;
    private readonly ILocalizedMessageProvider _messages;
    private readonly IClock _clock;
    private readonly ILogger<GatewayEmailChangeMailer> _logger;

    public GatewayEmailChangeMailer(
        IEmailGateway gateway,
        IOptionsMonitor<EmailOptions> options,
        IResilienceExecutor resilience,
        ILocalizedMessageProvider messages,
        IClock clock,
        ILogger<GatewayEmailChangeMailer> logger)
    {
        _gateway = gateway;
        _options = options;
        _resilience = resilience;
        _messages = messages;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Needs a public base URL as well as a transport, as verification does.</summary>
    public bool IsConfigured =>
        _gateway.IsConfigured
        && _options.CurrentValue.From.IsConfigured
        && Uri.TryCreate(_options.CurrentValue.AppBaseUrl, UriKind.Absolute, out _);

    public Task<bool> SendConfirmationAsync(EmailChangeLinkMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var link = $"{_options.CurrentValue.AppBaseUrl.TrimEnd('/')}{ConfirmPath}?token={Uri.EscapeDataString(message.Token)}";
        var hours = Hours(message.ExpiresAt);

        var subject = Text(IdentityMessageKeys.EmailChangeSubject, message.Language);
        var body = Format(IdentityMessageKeys.EmailChangeBody, message.Language, hours);
        var button = Text(IdentityMessageKeys.EmailChangeButton, message.Language);
        var footer = Text(IdentityMessageKeys.EmailChangeFooter, message.Language);

        return SendAsync(message.Address, subject, body, button, link, footer, "an email change link", cancellationToken);
    }

    public Task<bool> SendAddressUnavailableAsync(
        string address, string? language, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        return SendAsync(
            address,
            Text(IdentityMessageKeys.EmailChangeUnavailableSubject, language),
            Text(IdentityMessageKeys.EmailChangeUnavailableBody, language),
            button: null,
            link: null,
            footer: null,
            "an address-unavailable notice",
            cancellationToken);
    }

    public Task<bool> SendNoticeAsync(EmailChangeNotice notice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notice);

        var (subject, body) = notice.Stage == EmailChangeNoticeStage.Requested
            ? (Text(IdentityMessageKeys.EmailChangeRequestedSubject, notice.Language),
                Format(IdentityMessageKeys.EmailChangeRequestedBody, notice.Language, notice.MaskedNewAddress,
                    Hours(notice.LinkExpiresAt ?? _clock.UtcNow.AddHours(2))))
            : (Text(IdentityMessageKeys.EmailChangeCompletedSubject, notice.Language),
                Format(IdentityMessageKeys.EmailChangeCompletedBody, notice.Language, notice.MaskedNewAddress));

        return SendAsync(notice.Address, subject, body, button: null, link: null, footer: null,
            "an email change notice", cancellationToken);
    }

    private async Task<bool> SendAsync(
        string address,
        string subject,
        string body,
        string? button,
        string? link,
        string? footer,
        string what,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured) return false;

        var identity = _options.CurrentValue.SenderFor(SecurityCategoryKey);

        var outbound = new OutboundEmail
        {
            Recipients = [address],
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
                "{What} was accepted by {Provider} ({MessageId}).",
                what, dispatch.Provider, dispatch.ProviderMessageId ?? "no id");

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
            _logger.LogWarning(exception, "{Provider} did not carry {What}.", _gateway.Provider, what);
            return false;
        }
    }

    private int Hours(DateTimeOffset expiresAt) =>
        Math.Max(1, (int)Math.Round((expiresAt - _clock.UtcNow).TotalHours, MidpointRounding.AwayFromZero));

    private string Text(string key, string? language) => _messages.GetFor(key, language);

    private string Format(string key, string? language, params object[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, _messages.GetFor(key, language), arguments);
}
