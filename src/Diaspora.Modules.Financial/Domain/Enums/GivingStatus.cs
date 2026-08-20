namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Where a donation or a community contribution got to.
/// <para>
/// A non-monetary contribution reaches <see cref="Completed"/> without any payment: a pledge of six
/// Saturdays is complete when it is made, because nothing else has to happen for it to count.
/// </para>
/// </summary>
public enum GivingStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    Refunded = 4,
    Cancelled = 5
}
