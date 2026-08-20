using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Domain.Policies;

/// <summary>
/// Which moves one channel's delivery record may make.
/// <para>
/// Separate from the notification's own transitions because they answer different questions and
/// have different terminal states. A delivery that was suppressed is finished; the notification it
/// belongs to may still be opened, dismissed and archived afterwards.
/// </para>
/// </summary>
public static class DeliveryStatusTransitions
{
    public static bool IsTerminal(DeliveryStatus status) =>
        status is DeliveryStatus.Delivered
            or DeliveryStatus.Failed
            or DeliveryStatus.Suppressed
            or DeliveryStatus.Digested
            or DeliveryStatus.Unsupported;

    /// <summary>
    /// Whether an operator may ask for another attempt.
    /// <para>
    /// A failed delivery is retryable and a suppressed one is not, because suppression was a
    /// decision rather than a fault — retrying it would override a member's stated preference from
    /// an operations console, which is precisely the thing an operations console must not do.
    /// </para>
    /// </summary>
    public static bool IsRetryable(DeliveryStatus status) =>
        status is DeliveryStatus.Failed or DeliveryStatus.Retrying;

    public static bool CanTransition(DeliveryStatus from, DeliveryStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return to switch
        {
            DeliveryStatus.Deferred => from is DeliveryStatus.Pending,

            DeliveryStatus.Sending =>
                from is DeliveryStatus.Pending or DeliveryStatus.Deferred or DeliveryStatus.Retrying,

            DeliveryStatus.Delivered => from is DeliveryStatus.Sending or DeliveryStatus.Retrying,

            DeliveryStatus.Retrying => from is DeliveryStatus.Sending,

            DeliveryStatus.Failed =>
                from is DeliveryStatus.Sending or DeliveryStatus.Retrying or DeliveryStatus.Pending,

            DeliveryStatus.Suppressed => from is DeliveryStatus.Pending or DeliveryStatus.Deferred,

            DeliveryStatus.Digested => from is DeliveryStatus.Pending or DeliveryStatus.Deferred,

            DeliveryStatus.Unsupported => from is DeliveryStatus.Pending,

            _ => false
        };
    }
}
