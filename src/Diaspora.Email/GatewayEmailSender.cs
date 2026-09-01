using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Platform.Operations.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Email;

/// <summary>
/// The <see cref="IEmailSender"/> Notifications asks for, carried by a real vendor.
/// </summary>
/// <remarks>
/// <para>
/// This replaces <c>UnconfiguredEmailSender</c>, which was the only registration and answered
/// <c>IsConfigured => false</c> with a failure on every send. Every email the platform composed
/// was dropped at the wire — the 60 notification template seeds in <c>Catalog/Templates</c>, none
/// of them reachable.
/// </para>
/// <para>
/// Registered after Notifications so last-registration-wins replaces the placeholder, exactly as
/// the SMS adapter replaces its own.
/// </para>
/// </remarks>
public sealed class GatewayEmailSender : IEmailSender
{
    private readonly IEmailGateway _gateway;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly IResilienceExecutor _resilience;
    private readonly ILogger<GatewayEmailSender> _logger;

    public GatewayEmailSender(
        IEmailGateway gateway,
        IOptionsMonitor<EmailOptions> options,
        IResilienceExecutor resilience,
        ILogger<GatewayEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resilience);

        _gateway = gateway;
        _options = options;
        _resilience = resilience;
        _logger = logger;
    }

    /// <summary>
    /// Whether this deployment can actually send.
    /// </summary>
    /// <remarks>
    /// Both halves are required and both are load-bearing. Credentials without a verified sender
    /// address fail every send with a message about the <c>from</c> field, which reads like a
    /// bug in the message rather than a gap in configuration — so the sender identity is part of
    /// being configured, not an optional extra.
    /// </remarks>
    public bool IsConfigured => _gateway.IsConfigured && _options.CurrentValue.From.IsConfigured;

    public async Task<EmailSendResult> SendAsync(
        EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsConfigured)
        {
            // Permanent on purpose. A missing API key is not a blip, and marking it transient
            // would have the queue retry a configuration fault until the message expired.
            return EmailSendResult.Failure(
                "NotConfigured",
                $"'{EmailOptions.SectionName}' has no provider credentials or no sender address.",
                isPermanent: true);
        }

        var recipients = message.Recipients
            .Select(recipient => recipient.Address)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipients.Length == 0)
        {
            return EmailSendResult.Failure(
                "NoRecipients", "The message resolved to no email addresses.", isPermanent: true);
        }

        var sender = _options.CurrentValue.From;

        var outbound = new OutboundEmail
        {
            Recipients = recipients,
            Subject = message.Subject,
            Body = message.Body,

            // Notifications names the format; the vendor needs to know which field to put the
            // body in. Anything that is not explicitly HTML is sent as text, because a plain
            // body delivered as HTML renders as one unbroken paragraph.
            IsHtml = string.Equals(message.Format, "html", StringComparison.OrdinalIgnoreCase),
            FromAddress = sender.Address,
            FromName = sender.Name,
            UnsubscribeUrl = message.UnsubscribeToken is { Length: > 0 } token
                ? BuildUnsubscribeUrl(token)
                : null
        };

        var dependency = EmailDependencies.For(_gateway.Provider);

        try
        {
            var dispatch = await _resilience.ExecuteAsync(
                dependency,
                token => _gateway.SendAsync(outbound, token),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Notification {NotificationId} ({Category}) was accepted by {Provider} ({MessageId}).",
                message.NotificationId,
                message.CategoryKey,
                dispatch.Provider,
                dispatch.ProviderMessageId ?? "no id");

            return EmailSendResult.Success(dispatch.ProviderMessageId);
        }
        catch (EmailPermanentException exception)
        {
            // Not retried and not worth queueing again. A malformed address or an unverified
            // sending domain is refused identically on every attempt.
            _logger.LogError(
                "{Provider} will not carry notification {NotificationId}: {Reason}",
                _gateway.Provider, message.NotificationId, exception.Message);

            return EmailSendResult.Failure(
                "ProviderRejected", exception.Message, isPermanent: true);
        }
        catch (CircuitOpenException exception)
        {
            // The provider was not called at all. Transient by definition: the circuit closes.
            _logger.LogWarning(
                "The circuit for {Provider} is open until {ClosesAt:O}; it was not called.",
                _gateway.Provider, exception.ClosesAt);

            return EmailSendResult.Failure(
                "CircuitOpen", "The email provider is temporarily unavailable.");
        }
        catch (EmailTransientException exception)
        {
            // The retries are already spent by the time this surfaces. Reported as transient so
            // the queue decides whether to try again later, which is its decision, not this one's.
            _logger.LogWarning(
                "{Provider} did not accept notification {NotificationId}: {Reason}",
                _gateway.Provider, message.NotificationId, exception.Message);

            return EmailSendResult.Failure("ProviderUnavailable", exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host is shutting down or the caller gave up. Not a provider failure, and
            // recording it as one would open a breaker on a deployment.
            throw;
        }
    }

    /// <summary>
    /// The one-click unsubscribe link for a message that carries a token.
    /// </summary>
    /// <remarks>
    /// Sent as a <c>List-Unsubscribe</c> header rather than as body text, because that is what
    /// mail clients and spam filters read. Gmail and Outlook both require it of bulk senders, and
    /// a platform that sends notification email without one accumulates a reputation problem that
    /// is far harder to undo than to avoid.
    /// </remarks>
    private string BuildUnsubscribeUrl(string token) =>
        $"{_options.CurrentValue.UnsubscribeBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(token)}";
}
