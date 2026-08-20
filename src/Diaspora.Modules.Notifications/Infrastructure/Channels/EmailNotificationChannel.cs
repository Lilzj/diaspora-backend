using Diaspora.Modules.Notifications.Application.Preferences;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Infrastructure.Channels;

/// <summary>
/// Turns a notification into an email and hands it to whatever transport is registered.
/// <para>
/// The twin of <see cref="PushNotificationChannel"/>: the policy lives here, the wire lives behind
/// <see cref="IEmailSender"/>. The subject is the rendered title and the body is the rendered body,
/// because a template that wanted a different subject line already said so through a channel variant
/// — which is exactly what variants are for.
/// </para>
/// <para>
/// <b>HTML is never produced.</b> Plain text or Markdown reaches the sender, and an adapter that
/// wants a house-styled message wraps it. That keeps unsanitised markup out of a code path fed by
/// editable reference data.
/// </para>
/// </summary>
public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly IEmailSender _sender;
    private readonly UnsubscribeTokenFactory _unsubscribe;
    private readonly INotificationCategoryRepository _categories;
    private readonly ILogger<EmailNotificationChannel> _logger;

    public EmailNotificationChannel(
        IEmailSender sender,
        UnsubscribeTokenFactory unsubscribe,
        INotificationCategoryRepository categories,
        ILogger<EmailNotificationChannel> logger)
    {
        _sender = sender;
        _unsubscribe = unsubscribe;
        _categories = categories;
        _logger = logger;
    }

    public NotificationChannelKind Kind => NotificationChannelKind.Email;

    public bool IsAvailable => _sender.IsConfigured;

    public async Task<NotificationChannelOutcome> SendAsync(
        NotificationChannelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_sender.IsConfigured)
        {
            return NotificationChannelOutcome.NotConfigured();
        }

        if (request.Endpoints.Count == 0)
        {
            return NotificationChannelOutcome.NoEndpoint();
        }

        // B13.3. Minted per delivery rather than stored, so a token's lifetime is measured from the
        // email it travelled in — an unsubscribe link in a message from two years ago is not a live
        // credential. Null for a mandatory category: offering an unsubscribe the platform will then
        // refuse teaches a member that unsubscribes are ignored.
        var category = await _categories.FindByKeyAsync(request.CategoryKey, cancellationToken);

        var unsubscribeToken = category is { IsMandatory: false }
            ? _unsubscribe.Create(request.RecipientUserId, request.CategoryKey)
            : null;

        var message = new EmailMessage
        {
            Recipients = request.Endpoints,
            Subject = request.Title,
            Body = request.Body,
            UnsubscribeToken = unsubscribeToken,

            // Html is declared on the enum and refused by template validation, so this can only ever
            // be Plain or Markdown. Mapping it explicitly keeps the sender free of the domain enum.
            Format = request.Format == ContentFormat.Markdown ? "Markdown" : "Plain",
            Language = request.Language,
            Route = request.Route,
            NotificationId = request.NotificationId.Value,
            CategoryKey = request.CategoryKey
        };

        var result = await _sender.SendAsync(message, cancellationToken);

        if (result.Succeeded)
        {
            return NotificationChannelOutcome.Success(result.ProviderMessageId);
        }

        _logger.LogWarning(
            "Email delivery for notification {NotificationId} failed: {ReasonCode}",
            request.NotificationId.Value,
            result.ReasonCode);

        return result.IsPermanent
            ? NotificationChannelOutcome.Permanent(result.ReasonCode ?? "Rejected", result.Detail)
            : NotificationChannelOutcome.Transient(result.ReasonCode ?? "Unknown", result.Detail);
    }
}
