namespace Diaspora.Modules.Financial.Domain.Rewards;

/// <summary>What became of a grantor's request to take a grant back (D-9).</summary>
public enum RewardReversalOutcome
{
    /// <summary>The grant was pending; its points are gone.</summary>
    Reversed = 0,

    /// <summary>The grant had vested, or was never pending, and stays earned. The request is recorded.</summary>
    Declined = 1,

    /// <summary>This grant was already reversed or declined. Nothing changed.</summary>
    AlreadyDecided = 2,

    /// <summary>No grant on this account carries that reference.</summary>
    NotFound = 3
}
