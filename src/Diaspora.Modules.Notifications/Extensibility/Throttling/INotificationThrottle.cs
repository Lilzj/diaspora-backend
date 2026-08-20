using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Throttling;

/// <summary>
/// Decides whether a member has already had enough.
/// <para>
/// <b>Protects the member, not the platform.</b> The transport rate limiter protects the API from a
/// noisy client; this protects a person from a thread they are subscribed to that has just been
/// linked somewhere popular. Forty push notifications in a minute is not an attack — it is a working
/// system producing an outcome nobody wants — and no per-endpoint quota catches it, because every
/// one of those notifications is legitimate.
/// </para>
/// <para>
/// Critical is never throttled. A member being flooded with reactions is also a member who might be
/// being attacked, and the security alert is precisely the message that must get through.
/// </para>
/// </summary>
public interface INotificationThrottle
{
    /// <summary>
    /// Whether one more message may go to this member on this channel.
    /// <para>
    /// A refusal is not a failure: the caller records the delivery as suppressed with a reason, so
    /// the inbox still holds the message and the analytics still say what happened.
    /// </para>
    /// </summary>
    ValueTask<bool> TryAcquireAsync(
        UserId userId,
        NotificationChannelKind channel,
        NotificationPriority priority,
        CancellationToken cancellationToken = default);
}
