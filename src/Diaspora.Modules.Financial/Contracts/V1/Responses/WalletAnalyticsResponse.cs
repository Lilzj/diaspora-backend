namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// Money in and money out over a period, summed by the server.
/// <para>
/// <b>Grouped by currency and never totalled across them.</b> The platform does not convert, so a
/// single "total" would need an exchange rate it has no authority to pick. A client that saw one
/// would render a number nobody could reproduce.
/// </para>
/// <para>
/// <b>This exists because the alternative was paging a year of ledger entries to a phone.</b> The
/// analytics client did exactly that and reported the period as truncated past eight pages, which
/// works for a light user and is quietly wrong for a heavy one. Summing on the server is not an
/// analytics dashboard — it is the ledger answering a question about itself, which is the only place
/// that question has an authoritative answer.
/// </para>
/// </summary>
public sealed record WalletAnalyticsResponse
{
    public required DateTimeOffset PeriodStart { get; init; }

    public required DateTimeOffset PeriodEnd { get; init; }

    /// <summary>One row per currency, direction and entry type.</summary>
    public required IReadOnlyList<WalletAnalyticsTotalResponse> Totals { get; init; }

    /// <summary>What is in the wallets right now, per currency. Not a period figure.</summary>
    public required IReadOnlyList<WalletResponse> Wallets { get; init; }

    /// <summary>
    /// True when the period contains more entries than one pass summed, so a client can say so
    /// rather than presenting a partial total as a complete one.
    /// </summary>
    public required bool IsTruncated { get; init; }

    public required DateTimeOffset ComputedAt { get; init; }
}

/// <summary>One summed line: this much, this way, of this kind, in this currency.</summary>
public sealed record WalletAnalyticsTotalResponse
{
    public required string Currency { get; init; }

    /// <summary><c>Credit</c> or <c>Debit</c>.</summary>
    public required string Direction { get; init; }

    /// <summary>The ledger entry type. <c>Deposit</c>, <c>Payment</c>, <c>Refund</c>, <c>Payout</c>.</summary>
    public required string EntryType { get; init; }

    public required MoneyResponse Amount { get; init; }

    public required int Count { get; init; }
}
