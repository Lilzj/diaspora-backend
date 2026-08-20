namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Who caused a financial action. Part of every ledger entry's audit stamp.</summary>
public enum FinancialActorType
{
    /// <summary>The account owner, acting for themselves.</summary>
    Member = 1,

    /// <summary>Platform staff, acting on somebody else's account.</summary>
    Staff = 2,

    /// <summary>A sweep, a consumer or a scheduled job. No human.</summary>
    System = 3,

    /// <summary>A gateway callback. The actor is outside the platform.</summary>
    Gateway = 4
}
