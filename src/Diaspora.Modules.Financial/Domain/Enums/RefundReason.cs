namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Why money went back. Carried in the ledger entry and in the published contract.</summary>
public enum RefundReason
{
    RequestedByPayer = 1,
    Duplicate = 2,
    Fraudulent = 3,
    NotDelivered = 4,

    /// <summary>A policy said so — a placement cancelled inside its refund window.</summary>
    PolicyRule = 5,

    Goodwill = 6
}
