namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Where a member's refund request has got to.
/// <para>
/// The two states the client's <c>RefundStatus</c> union was missing — a request that has been made
/// and a request that has been turned down — and they belong on the request rather than on the
/// refund, because a rejected request never produces a refund to carry a status at all.
/// </para>
/// </summary>
public enum RefundRequestStatus
{
    /// <summary>Raised by the member. Nothing has moved.</summary>
    Requested = 1,

    /// <summary>A staff member agreed. The refund itself is a separate, later fact.</summary>
    Approved = 2,

    /// <summary>A staff member declined, with a member-safe reason.</summary>
    Rejected = 3,

    /// <summary>Withdrawn by the member before anybody decided.</summary>
    Withdrawn = 4
}
