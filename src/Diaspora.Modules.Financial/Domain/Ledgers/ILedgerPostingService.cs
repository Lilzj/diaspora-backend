using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Domain.Ledgers;

/// <summary>
/// The one door into the ledger.
/// <para>
/// <b>Exactly one implementation exists, and an architecture test asserts it.</b> Every movement of
/// money on this platform — a capture, a refund, a commission, a credit grant, a donation, a
/// subscription charge — goes through this interface. A second implementation would be a path where
/// the balance check, the sequence assignment, the wallet status check and the balancing rule are
/// not applied, and the enforcement is only ever as good as the number of ways around it.
/// </para>
/// <para>
/// <b>It stages; it does not save.</b> The caller owns the unit of work, because a posting is almost
/// never the only thing a command does — a purchase writes a purchase row, a placement and a posting,
/// and all three must land together or none of them. Commands that post therefore declare
/// <c>[Transactional]</c> on the request.
/// </para>
/// </summary>
public interface ILedgerPostingService
{
    /// <summary>
    /// Appends a balanced set of entries and stages the transaction.
    /// <para>
    /// Returns a failure rather than throwing for every expected refusal — unbalanced legs, a frozen
    /// wallet, insufficient funds, a currency mismatch — because each of those is a different message
    /// on the client and a different action for the member.
    /// </para>
    /// </summary>
    Task<Result<LedgerPostingResult>> PostAsync(
        LedgerPostingRequest request, CancellationToken cancellationToken = default);
}
