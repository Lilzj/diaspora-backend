namespace Diaspora.Modules.Financial.Extensibility.Settlement;

/// <summary>
/// Where provider payouts happen.
/// <para>
/// <b>No implementation ships in this assembly, and an architecture test keeps it that way.</b> A
/// deployment registers an adapter — see <c>Diaspora.Payments.Stripe</c> — and the ledger, the
/// wallets, the clearing counterparty and the revenue splits it needs are all already here.
/// </para>
/// <para>
/// <b>It answers with a <see cref="SettlementOutcome"/> rather than a boolean, and that is the whole
/// safety property.</b> A boolean forced "we timed out" to be reported as either paid or refused;
/// it was reported as refused, so the platform released the hold, and a provider that had actually
/// sent the money left the member paid and their balance restored. An adapter that does not know
/// says so.
/// </para>
/// </summary>
public interface ISettlementProvider
{
    string Provider { get; }

    /// <summary>
    /// Sends the instruction, and reports what is actually known about it afterwards.
    /// <para>
    /// <b>Never throw to mean "refused".</b> A caller cannot tell a network failure from a
    /// declined transfer by catching, so an adapter that throws has said "unknown" — which the
    /// sweep treats as money possibly gone. Return <see cref="SettlementOutcome.Refused"/> only on
    /// a positive refusal from the provider.
    /// </para>
    /// </summary>
    Task<SettlementOutcome> SubmitAsync(
        SettlementInstruction instruction, CancellationToken cancellationToken = default);
}

/// <summary>
/// Asks a provider what became of an instruction it was already given.
/// <para>
/// <b>This is how an unknown outcome stops being unknown.</b> A withdrawal left in
/// <c>Processing</c> is money held against a payout that may or may not have happened, and without
/// somewhere to ask, it stays that way until a person notices. Registering this makes the
/// resolution automatic; not registering it leaves the withdrawal held and visible, which is the
/// safe half of the problem rather than the whole of it.
/// </para>
/// <para>
/// Optional, and separate from <see cref="ISettlementProvider"/> on purpose: a provider that cannot
/// be asked after the fact is still a usable payout rail, and forcing every adapter to implement a
/// query it cannot answer would produce adapters that guess.
/// </para>
/// </summary>
public interface ISettlementStatusProvider
{
    /// <summary>The adapter this answers for. Matched against the withdrawal's settlement provider.</summary>
    string Provider { get; }

    /// <summary>
    /// What became of the instruction carrying <paramref name="reference"/> — the same reference
    /// that was submitted, which for a withdrawal is its own identifier.
    /// <para>
    /// Returns <see cref="SettlementOutcome.Unresolved"/> when the provider cannot be reached or
    /// says nothing useful. It must never report <see cref="SettlementOutcome.Refused"/> for an
    /// instruction it simply cannot find unless the provider positively says it does not exist:
    /// "no record" and "not created" are the same sentence and very different facts.
    /// </para>
    /// </summary>
    Task<SettlementOutcome> GetOutcomeAsync(
        string reference, CancellationToken cancellationToken = default);
}
