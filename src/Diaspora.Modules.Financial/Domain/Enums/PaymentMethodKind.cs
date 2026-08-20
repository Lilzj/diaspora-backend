namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// How money is moving, in provider-independent terms.
/// <para>
/// Deliberately the shape of the instrument rather than the name of a provider. Mobile money is one
/// kind here whether the adapter behind it is Nigerian, Kenyan or Ghanaian.
/// </para>
/// </summary>
public enum PaymentMethodKind
{
    Unknown = 0,
    Card = 1,
    BankTransfer = 2,
    MobileMoney = 3,

    /// <summary>Paid from an existing platform balance. No gateway involved.</summary>
    Wallet = 4,

    /// <summary>Recorded by an operator against a transfer that happened outside the platform.</summary>
    ManualTransfer = 5,

    /// <summary>Paid with granted credit.</summary>
    Credit = 6
}
