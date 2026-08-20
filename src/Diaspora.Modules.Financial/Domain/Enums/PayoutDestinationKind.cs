namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// What sort of thing money is sent to.
/// <para>
/// Deliberately coarse. The platform stores <em>which kind</em> and a masked label, and nothing
/// else — the credential itself lives with the settlement provider that will use it, because a
/// platform that never holds an account number cannot leak one.
/// </para>
/// </summary>
public enum PayoutDestinationKind
{
    BankAccount = 1,
    MobileMoney = 2,
    Card = 3,

    /// <summary>Whatever a settlement provider supports that this list does not name.</summary>
    ProviderManaged = 99
}
