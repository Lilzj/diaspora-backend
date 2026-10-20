using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>Retrieval and staging for donations.</summary>
public interface IDonationRepository
{
    void Add(Donation donation);

    void Update(Donation donation);

    Task<Donation?> FindAsync(DonationId donationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The only lookup an anonymous donor has. The receipt reference is deliberately the key.
    /// </summary>
    Task<Donation?> FindByReceiptAsync(
        string receiptReference, CancellationToken cancellationToken = default);

    Task<Donation?> FindByPaymentIntentAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default);

    /// <summary>A donor's own history. Anonymous donations are absent by construction.</summary>
    Task<IReadOnlyList<Donation>> ListByDonorAsync(
        Guid donorUserId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// One donor's own donations to one cause, newest first.
    /// <para>
    /// <b>Keyed on the donor, so anonymous donations are absent by construction</b> — the same rule
    /// as <see cref="ListByDonorAsync"/>. An anonymous donation stores no donor, and finding it again
    /// through the payment's payer would undo the anonymity the donor asked for; its receipt
    /// reference stays the only key to it.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Donation>> ListByDonorForCauseAsync(
        Guid donorUserId,
        DonationCauseId causeId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Donation>> ListByCauseAsync(
        DonationCauseId causeId,
        GivingStatus? status,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>The reconciliation input: what a cause has actually received.</summary>
    Task<(long RaisedMinorUnits, int SupporterCount)> TotalsForCauseAsync(
        DonationCauseId causeId, CancellationToken cancellationToken = default);

    // ── B15.1 · COMMUNITY-LIFE-GAP-007 ───────────────────────────────────────────────────────

    /// <summary>
    /// A cause's completed donations, newest first, cursor-paged. The supporter list's source.
    /// <para>
    /// <b>Completed only, and that is a financial statement rather than a filter for tidiness.</b> A
    /// pending donation is money that has not arrived; listing it as a supporter would publish a
    /// figure that can still fail, and the register's whole objection to the client accumulating
    /// totals is that they would not reconcile.
    /// </para>
    /// <para>
    /// The cursor is ADR-017's — a boundary instant plus the identifiers already served at it —
    /// because <c>CreatedAt</c> is stamped per unit of work and a strict inequality drops the tie.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Donation>> ListSupportersAsync(
        DonationCauseId causeId,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A cause's completed giving, bucketed by period, computed in the database.
    /// <para>
    /// <b>Aggregated in SQL rather than by materialising the donations.</b> A cause with fifty
    /// thousand donations would otherwise pull fifty thousand rows into memory to add them up, and
    /// the client is asking for at most a few dozen numbers.
    /// </para>
    /// <para>
    /// <b>Buckets are UTC day boundaries, and the response says so.</b> A platform spanning eleven
    /// time zones has no single local midnight to group on, and picking the caller's would make two
    /// people disagree about which day a donation happened on.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<DonationPeriodTotal>> TimeSeriesForCauseAsync(
        DonationCauseId causeId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What a cause has taken, refused and given back over a window. The time series' footer.
    /// <para>
    /// Failed and refunded are counted separately and never netted into the raised figure, because
    /// "we raised ten thousand" and "we raised ten thousand and gave two back" are different
    /// sentences and the second is the true one.
    /// </para>
    /// </summary>
    Task<DonationWindowSummary> SummariseForCauseAsync(
        DonationCauseId causeId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

/// <summary>One bucket of a cause's giving. UTC day, and the day is the key.</summary>
public sealed record DonationPeriodTotal
{
    /// <summary>The UTC date this bucket covers, at midnight.</summary>
    public required DateTimeOffset PeriodStart { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required int DonationCount { get; init; }
}

/// <summary>What happened to a cause's giving over a window, by outcome.</summary>
public sealed record DonationWindowSummary
{
    public required long CompletedMinorUnits { get; init; }

    public required int CompletedCount { get; init; }

    public required long RefundedMinorUnits { get; init; }

    public required int RefundedCount { get; init; }

    public required int FailedCount { get; init; }

    /// <summary>Distinct named donors plus every anonymous gift. Anonymous gifts cannot be deduplicated.</summary>
    public required int SupporterCount { get; init; }

    /// <summary>Every currency seen in the window. More than one means the cause was misconfigured.</summary>
    public required IReadOnlyList<string> Currencies { get; init; }
}
