namespace Diaspora.Modules.Financial.Extensibility.Settlement;

/// <summary>
/// Where provider payouts would happen.
/// <para>
/// <b>Declared with no implementation, and the brief says so explicitly: "do not implement
/// settlement engines; design extension seams".</b> The payout endpoint answers
/// <c>financial.payout.not_supported</c>, which is a great deal more useful than a 404 on a route
/// somebody expects to exist.
/// </para>
/// <para>
/// The interface exists so that the day a payout engine lands, the ledger, the wallets, the clearing
/// counterparty and the revenue splits it needs are all already here and none of them changes.
/// </para>
/// </summary>
public interface ISettlementProvider
{
    string Provider { get; }

    Task<bool> SubmitAsync(
        SettlementInstruction instruction, CancellationToken cancellationToken = default);
}
