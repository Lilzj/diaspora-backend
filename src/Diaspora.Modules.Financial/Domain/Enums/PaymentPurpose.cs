namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// What a payment is for, in the only sense the ledger cares about: where the money lands.
/// <para>
/// <b>This is not the subject.</b> <c>SubjectType</c> says which listing, event or organisation a
/// payment relates to and the ledger does nothing with it. The purpose decides the counterparty —
/// whether captured money becomes platform revenue or the payer's own balance — and there are
/// exactly two answers, because a third would mean a third pair of legs somebody has to get right.
/// </para>
/// <para>
/// <b>No wallet identifier is ever accepted from a client.</b> A deposit credits the payer's own
/// primary wallet, resolved server-side from the payer on the intent. An enum member plus the payer
/// is not something a request body can point somewhere else.
/// </para>
/// </summary>
public enum PaymentPurpose
{
    /// <summary>
    /// Money the platform is taking: a purchase, a subscription, a donation, a contribution.
    /// Captured to the platform's revenue wallet. The default, and what every payment before B15.1
    /// was.
    /// </summary>
    Purchase = 1,

    /// <summary>
    /// Money the payer is putting into their own balance. Captured to the payer's own primary wallet
    /// in the intent's currency.
    /// </summary>
    Deposit = 2
}
