namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Whether a financial identity may transact at all.</summary>
public enum FinancialAccountStatus
{
    Pending = 1,

    Active = 2,

    /// <summary>Under review. Existing wallets keep their balances and refuse new debits.</summary>
    Restricted = 3,

    Closed = 4
}
