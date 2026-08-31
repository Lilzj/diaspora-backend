namespace Diaspora.Modules.Financial.Extensibility.Payouts;

/// <summary>
/// One line of a provider's own record of a payout it made.
/// <para>
/// <b><see cref="Reference"/> is the platform's, not the provider's, and that is what makes the
/// check worth running.</b> Matching on the provider's identifier would only ever find payouts the
/// platform already knows about — the useful finding is the opposite one: a transfer the provider
/// made that no withdrawal here accounts for.
/// </para>
/// </summary>
public sealed record PayoutReconciliationRecord
{
    public required string Provider { get; init; }

    /// <summary>The provider's own handle for the movement.</summary>
    public required string ProviderReference { get; init; }

    /// <summary>
    /// The platform's reference, as the provider echoes it back. Null when the provider holds none
    /// — which is itself a finding, because every payout this platform makes carries one.
    /// </summary>
    public string? Reference { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>
    /// The provider's own status, normalised to the platform's three outcomes by the adapter:
    /// <c>paid</c>, <c>failed</c> or <c>reversed</c>. Anything else the adapter leaves out.
    /// </summary>
    public required string Status { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// Fetches a provider's own record of the payouts it made in a window.
/// <para>
/// <b>The seam that answers "did we send anything we do not know about".</b>
/// <c>ISettlementStatusProvider</c> resolves a withdrawal the platform already has a question about;
/// this asks the opposite question, and it is the one nobody asks until an accountant does. A
/// transfer the provider made with no withdrawal behind it is money that left the platform's
/// account without the platform recording it — the single most serious thing a payout system can
/// get wrong, and invisible to every other check here.
/// </para>
/// <para>
/// <b>No implementation ships.</b> Like every other provider seam in this module, a deployment
/// registers one. The sweep exports its counter either way, so a deployment can see that
/// reconciliation is not running rather than assuming it is.
/// </para>
/// </summary>
public interface IPayoutReconciliationProvider
{
    string Provider { get; }

    /// <summary>
    /// Everything the provider paid out in a window, oldest first.
    /// <para>
    /// Implementations page internally and return the whole window: a caller that had to page would
    /// have to know each provider's cursor shape, which is the knowledge this seam exists to keep
    /// on the other side of it.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PayoutReconciliationRecord>> FetchAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
