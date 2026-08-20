namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// Everything a member is currently entitled to, and how much of it they have used.
/// <para>
/// <b>The ceiling is B15's and the consumption is not, and the response says which is which by
/// leaving the second out where nobody reported it.</b> A plan says <c>listings.concurrent: 15</c>;
/// only Marketplace knows how many listings somebody has open. A counter with no consumption draws no
/// bar on the client, because an empty bar reads as "none used" — a specific claim nobody made.
/// </para>
/// <para>
/// <b>Resolved server-side, always.</b> A client's own entitlement decision is a suggestion; this is
/// the answer, and every route that gates on an entitlement re-reads it rather than trusting what the
/// client was told.
/// </para>
/// </summary>
public sealed record EntitlementSnapshotResponse
{
    /// <summary>What the member has. Keys and optional limits.</summary>
    public required IReadOnlyList<EntitlementResponse> Entitlements { get; init; }

    /// <summary>How much of each has been used, where a vertical reported it.</summary>
    public required IReadOnlyList<UsageCounterResponse> Counters { get; init; }

    /// <summary>Which plan granted them. Null when the member is on the free tier.</summary>
    public string? SourcePlanCode { get; init; }

    /// <summary>The subscription behind them, when there is one.</summary>
    public Guid? SubscriptionId { get; init; }

    /// <summary>
    /// When the entitlements lapse if nothing renews. The client shows a date rather than computing
    /// one.
    /// </summary>
    public DateTimeOffset? ValidUntil { get; init; }

    public required DateTimeOffset ResolvedAt { get; init; }
}

/// <summary>
/// One limit and what is left of it.
/// <para>
/// <b><see cref="AtLimitBehaviour"/> is required because a client must not assume refusal.</b> Some
/// limits refuse, some queue, some charge for the overage — and a meter showing 15/15 without saying
/// which is telling a member their next listing will fail when in fact it will cost them.
/// </para>
/// </summary>
public sealed record UsageCounterResponse
{
    public required string Key { get; init; }

    /// <summary>The ceiling, from the plan. Null when the entitlement is a capability, not a limit.</summary>
    public long? Limit { get; init; }

    /// <summary>How many are used. Null means nobody measured it — not zero.</summary>
    public long? Consumed { get; init; }

    /// <summary>Limit minus consumed, when both are known.</summary>
    public long? Remaining { get; init; }

    /// <summary><c>Refuse</c>, <c>Queue</c> or <c>Overage</c>.</summary>
    public string? AtLimitBehaviour { get; init; }

    public DateTimeOffset? PeriodStart { get; init; }

    public DateTimeOffset? PeriodEnd { get; init; }
}
