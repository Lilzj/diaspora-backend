namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Where a payout request has got to.
/// <para>
/// <b>Every one of these is a state the money is genuinely in, not a label on a spinner.</b>
/// <c>Requested</c> means the funds are held and nobody has picked it up; <c>Processing</c> means a
/// settlement provider has been handed the instruction and the outcome is unknown; <c>Completed</c>
/// means it left. The gap between the last two is where a payout that has been sent and not confirmed
/// lives, and collapsing them would make "did my withdrawal go through" unanswerable at exactly the
/// moment somebody asks.
/// </para>
/// </summary>
public enum WithdrawalStatus
{
    /// <summary>Raised, and the amount held out of the available balance. Cancellable.</summary>
    Requested = 1,

    /// <summary>Handed to a settlement provider. No longer cancellable by the member.</summary>
    Processing = 2,

    /// <summary>The provider confirmed it. The hold became a payout and the money is gone.</summary>
    Completed = 3,

    /// <summary>The provider refused it. The hold was released back to available.</summary>
    Failed = 4,

    /// <summary>Withdrawn by the member before anybody picked it up. The hold was released.</summary>
    Cancelled = 5,

    /// <summary>
    /// Completed and then returned — a bounced transfer, a recalled payment. The money came back and
    /// the ledger says so in a compensating entry rather than by undoing the payout.
    /// </summary>
    Reversed = 6
}
