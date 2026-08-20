namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Where a payment intent is in its lifecycle.
/// <para>
/// <b>Every transition is forward and none is destructive.</b> A failed intent is not deleted and a
/// cancelled one is not reused; the aggregate refuses an illegal transition rather than silently
/// accepting it, because a payment that quietly went backwards is the defect nobody finds until a
/// reconciliation two months later.
/// </para>
/// </summary>
public enum PaymentIntentStatus
{
    /// <summary>Being assembled. No gateway has seen it.</summary>
    Draft = 1,

    /// <summary>Handed to a gateway and awaiting an outcome.</summary>
    Pending = 2,

    /// <summary>The funds are reserved with the issuer but not taken.</summary>
    Authorized = 3,

    /// <summary>Taken. The money is the platform's and the ledger says so.</summary>
    Captured = 4,

    /// <summary>Reconciled against the provider's own record.</summary>
    Settled = 5,

    Failed = 6,

    Cancelled = 7,

    /// <summary>Fully refunded. A partial refund leaves the intent captured or settled.</summary>
    Refunded = 8,

    /// <summary>Nobody completed it in time.</summary>
    Expired = 9
}
