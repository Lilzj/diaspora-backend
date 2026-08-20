using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>
/// A transport a message can travel on.
/// <para>
/// <b>The module's central seam, and the whole of "no vendor lock-in".</b> Everything above this
/// interface — routing, preferences, quiet hours, digests, retries, analytics — is written once and
/// knows nothing about how a message physically moves. Adding SMS is a class implementing this and
/// a registration; it touches no aggregate, no handler and no contract.
/// </para>
/// <para>
/// <b>A channel never decides whether to send.</b> By the time one is called, the rule has matched,
/// the preferences have been consulted, quiet hours have been applied and a delivery row exists. A
/// channel that consulted a preference would be a second place the answer could differ from the
/// first.
/// </para>
/// <para>
/// <b>A channel never throws for an ordinary failure.</b> A dead push token, a rejected address and
/// a provider timeout are all outcomes, and the retry ladder is driven by
/// <see cref="NotificationChannelOutcome.IsPermanent"/>. Throwing is reserved for programming
/// errors, and costs the whole dispatch batch rather than one message.
/// </para>
/// </summary>
public interface INotificationChannel
{
    /// <summary>Which kind this channel serves. Exactly one channel per kind may be registered.</summary>
    NotificationChannelKind Kind { get; }

    /// <summary>
    /// Whether the channel has what it needs to work — a configured provider, a signing key.
    /// <para>
    /// Consulted before a delivery is attempted, so an unconfigured channel records
    /// <c>Unsupported</c> once rather than failing eight times on a retry ladder.
    /// </para>
    /// </summary>
    bool IsAvailable { get; }

    Task<NotificationChannelOutcome> SendAsync(
        NotificationChannelRequest request, CancellationToken cancellationToken = default);
}
