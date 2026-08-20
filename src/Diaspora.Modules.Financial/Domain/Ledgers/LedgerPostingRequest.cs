using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Domain.Ledgers;

/// <summary>
/// Everything needed to move money, in one value.
/// <para>
/// <b>The legs must balance, per currency and per direction.</b> The posting service refuses
/// otherwise, and that refusal is the invariant the whole platform's financial correctness rests on:
/// a ledger where credits and debits can disagree is a ledger nobody can check, which makes every
/// number derived from it an assertion rather than a fact.
/// </para>
/// <para>
/// The platform's clearing wallet is what makes money arriving from outside balance. See
/// <c>WalletType.Clearing</c>.
/// </para>
/// </summary>
public sealed record LedgerPostingRequest
{
    public required TransactionType TransactionType { get; init; }

    /// <summary>The headline figure a member sees on a statement line.</summary>
    public required Money Amount { get; init; }

    public required string Description { get; init; }

    /// <summary>Actor, source, reason, correlation, request and the policy version in force.</summary>
    public required AuditStamp Audit { get; init; }

    public required IReadOnlyList<LedgerPostingLeg> Legs { get; init; }

    public SubjectReference? Subject { get; init; }

    public Guid? InitiatedByUserId { get; init; }

    /// <summary>
    /// The caller's idempotency key. Uniquely indexed on the transaction, so a replay is refused by
    /// the database rather than by a check a refactor can step around.
    /// </summary>
    public string? Reference { get; init; }

    public string? CountryCode { get; init; }
}
