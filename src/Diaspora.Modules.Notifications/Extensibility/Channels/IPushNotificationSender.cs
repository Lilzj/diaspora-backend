namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>
/// The transport behind the push channel.
/// <para>
/// <b>Separate from <see cref="INotificationChannel"/> on purpose, and the separation is what the
/// brief means by "push abstraction".</b> The channel owns the policy — how a notification becomes a
/// payload, which endpoints to use, what a dead token means. The sender owns the wire. Swapping
/// Firebase for APNs, or adding a second provider for one platform, replaces this and leaves the
/// channel, its tests and its analytics untouched.
/// </para>
/// <para>
/// No implementation with a vendor in it ships in this phase. The registered implementation records
/// the attempt and reports that nothing is configured, which exercises the entire path above it.
/// </para>
/// </summary>
public interface IPushNotificationSender
{
    /// <summary>Whether a provider is configured. False for the shipped implementation.</summary>
    bool IsConfigured { get; }

    Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default);
}
