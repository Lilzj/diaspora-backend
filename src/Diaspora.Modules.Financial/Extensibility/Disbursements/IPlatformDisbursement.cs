using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Extensibility.Disbursements;

/// <summary>
/// Paying a member money the platform is holding for them.
/// <para>
/// <b>The seam a module reaches for when it decides somebody is owed something.</b> A creator's
/// earning, a grant, a settled prize: another module knows <em>that</em> money is owed and
/// <em>why</em>, and B15 knows how to move it. This is the only way the second half happens — a
/// module cannot reach <c>ILedgerPostingService</c>, and a second path into the ledger would be a
/// path where the balancing rule, the wallet status check and the sequence assignment are not
/// applied.
/// </para>
/// <para>
/// <b>It is the opposite direction to a payment.</b> A capture moves member money into platform
/// revenue; this moves platform revenue back out to a member. It is not a payout <em>out of
/// Diaspora</em> — that is a <c>Withdrawal</c>, which the member raises against the balance this
/// puts there.
/// </para>
/// <para>
/// <b>Idempotent on <see cref="PlatformDisbursementRequest.Reference"/>.</b> A caller that crashes
/// between paying and recording that it paid will call again, and must not pay twice. The ledger
/// answers the existing transaction rather than posting a second one.
/// </para>
/// </summary>
public interface IPlatformDisbursement
{
    /// <summary>
    /// Moves the amount from the platform's revenue wallet into the member's own.
    /// <para>
    /// Fails rather than throws for every expected refusal — no platform wallet in that currency,
    /// a frozen member wallet, a revenue balance that does not cover it — because a caller sweeping
    /// a batch has to be able to skip one and carry on.
    /// </para>
    /// </summary>
    Task<Result<PlatformDisbursementReceipt>> PayAsync(
        PlatformDisbursementRequest request, CancellationToken cancellationToken = default);
}

/// <summary>What is owed, to whom, and on what grounds.</summary>
public sealed record PlatformDisbursementRequest
{
    /// <summary>The member being paid. Their account and wallet are opened if they have none.</summary>
    public required Guid BeneficiaryUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required int Exponent { get; init; }

    /// <summary>
    /// The caller's own identifier for this obligation, and the idempotency key.
    /// <para>
    /// Namespaced by the caller — <c>earning:{id:N}</c> — so two modules cannot collide on a
    /// counter. A second call with the same reference answers the first transaction.
    /// </para>
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>What the member will read on the transaction.</summary>
    public required string Description { get; init; }

    /// <summary>Where it came from, for the audit trail.</summary>
    public string? Reason { get; init; }
}

/// <summary>The movement that was made, or the one that already had been.</summary>
public sealed record PlatformDisbursementReceipt
{
    public required Guid TransactionId { get; init; }

    /// <summary>
    /// False when the reference had already been paid.
    /// <para>
    /// Told apart deliberately: a sweep that treats a replay as a fresh payment would log a batch
    /// twice its real size, and one that treats it as a failure would retry for ever.
    /// </para>
    /// </summary>
    public required bool Posted { get; init; }
}
