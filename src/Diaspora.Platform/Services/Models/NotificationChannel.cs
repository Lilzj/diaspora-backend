namespace Diaspora.Platform.Services.Models;

/// <summary>
/// <b>The kernel's channel flags, superseded by B13's own.</b> Reachable only through
/// <see cref="NotificationRequest"/>, obsoleted with it in B1.4.1, and not to be confused with
/// <c>Diaspora.Modules.Notifications.Domain.NotificationChannelKind</c> — which is the one the
/// Communication Platform actually routes on, and which knows about preferences, quiet hours and
/// per-channel delivery state that a flags enum cannot express.
/// </summary>
[Flags]
[Obsolete(
    "NotificationChannel belongs to the unimplemented INotificationPublisher port. The Communication " +
    "Platform (B13) routes on NotificationChannelKind, resolved from the recipient's preferences " +
    "rather than chosen by the caller. Publish an integration event through " +
    "IIntegrationEventPublisher instead. See docs/notifications/architecture.md.",
    error: true,
    DiagnosticId = "DSP2004")]
public enum NotificationChannel
{
    InApp = 1,
    Push = 2,
    Email = 4,
    Sms = 8,
    Webhook = 16
}
