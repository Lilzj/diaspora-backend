namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Where a subscription is in its lifecycle.
/// <para>
/// The brief lists <c>Renewed</c> in the same sequence. It is not a state — a renewed subscription
/// is <see cref="Active"/> with a later period end — so it is published as an event and never
/// stored as a status. A status meaning "active, and something happened recently" is a status
/// nothing can usefully query on.
/// </para>
/// </summary>
public enum SubscriptionStatus
{
    Trial = 1,
    Active = 2,

    /// <summary>Payment failed and access continues while it is retried.</summary>
    GracePeriod = 3,

    /// <summary>Access withdrawn, the subscription still exists and can be revived.</summary>
    Suspended = 4,

    Expired = 5,
    Cancelled = 6
}
