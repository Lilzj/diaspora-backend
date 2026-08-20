using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Application.Dispatch;

/// <summary>
/// What one channel should do with one message, decided before anything is written.
/// <para>
/// The output of the delivery policy resolver, and the reason that resolver is a single class: the
/// answer depends on the rule, the category, the member's channel switch, their category switch,
/// their quiet hours, their do-not-disturb, their digest schedule, the priority and whether a
/// transport is even configured. Nine inputs producing one of five outcomes is exactly the kind of
/// decision that goes wrong when it is spread over four call sites.
/// </para>
/// </summary>
public sealed record ChannelPlan
{
    public required NotificationChannelKind Channel { get; init; }

    /// <summary>What the delivery row should be created as.</summary>
    public required DeliveryStatus Outcome { get; init; }

    /// <summary>
    /// The earliest moment the channel may act. Later than now for anything quiet hours deferred,
    /// for a scheduled rule, or for a digest.
    /// </summary>
    public required DateTimeOffset DeliverAfter { get; init; }

    /// <summary>Why, when the outcome is a suppression. Recorded on the delivery row.</summary>
    public string? ReasonCode { get; init; }

    public static ChannelPlan Send(NotificationChannelKind channel, DateTimeOffset deliverAfter) =>
        new() { Channel = channel, Outcome = DeliveryStatus.Pending, DeliverAfter = deliverAfter };

    public static ChannelPlan Defer(NotificationChannelKind channel, DateTimeOffset until) =>
        new() { Channel = channel, Outcome = DeliveryStatus.Deferred, DeliverAfter = until };

    public static ChannelPlan Suppress(
        NotificationChannelKind channel, string reasonCode, DateTimeOffset now) =>
        new()
        {
            Channel = channel,
            Outcome = DeliveryStatus.Suppressed,
            DeliverAfter = now,
            ReasonCode = reasonCode
        };

    public static ChannelPlan Digest(NotificationChannelKind channel, DateTimeOffset now) =>
        new() { Channel = channel, Outcome = DeliveryStatus.Digested, DeliverAfter = now };

    public static ChannelPlan Unsupported(NotificationChannelKind channel, DateTimeOffset now) =>
        new() { Channel = channel, Outcome = DeliveryStatus.Unsupported, DeliverAfter = now };
}
