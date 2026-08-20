namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>
/// The transport behind the email channel.
/// <para>
/// The twin of <see cref="IPushNotificationSender"/> and separate from the email channel for the
/// same reason: the channel owns how a notification becomes a message, the sender owns the wire.
/// SES, SendGrid, Postmark and an SMTP relay are all one implementation of this and no change
/// anywhere else.
/// </para>
/// <para>
/// No vendor implementation ships in this phase.
/// </para>
/// </summary>
public interface IEmailSender
{
    /// <summary>Whether a provider is configured. False for the shipped implementation.</summary>
    bool IsConfigured { get; }

    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
