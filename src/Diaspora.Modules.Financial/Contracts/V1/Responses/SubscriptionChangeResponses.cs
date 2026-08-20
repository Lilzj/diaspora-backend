namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// What changing plan would cost and what it would change.
/// <para>
/// <b>Server-computed, and the reason is arithmetic rather than policy.</b> A client that multiplies
/// a plan price by the remaining fraction of a period will disagree with the server at a rounding
/// boundary, and the figure it gets wrong is the one it charges somebody.
/// </para>
/// <para>
/// <b>The entitlement deltas are what the screen actually renders.</b> A member changing plan wants
/// to know what they gain and what they lose, and computing that on the device would mean the client
/// holding the plan catalogue's semantics.
/// </para>
/// </summary>
public sealed record PlanChangePreviewResponse
{
    public required string FromPlanCode { get; init; }

    public required string ToPlanCode { get; init; }

    /// <summary><c>Upgrade</c>, <c>Downgrade</c> or <c>IntervalChange</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>What is charged now. Zero for a downgrade, which waits for the period boundary.</summary>
    public required MoneyResponse DueNow { get; init; }

    /// <summary>What the unused remainder of the current period was worth.</summary>
    public required MoneyResponse ProratedCredit { get; init; }

    /// <summary>What the next ordinary charge will be.</summary>
    public required MoneyResponse NextChargeAmount { get; init; }

    public DateTimeOffset? NextChargeAt { get; init; }

    /// <summary>When the change takes effect. Now for an upgrade, the period end for a downgrade.</summary>
    public required DateTimeOffset EffectiveAt { get; init; }

    public required IReadOnlyList<string> GainedEntitlementKeys { get; init; }

    public required IReadOnlyList<string> LostEntitlementKeys { get; init; }

    public required bool IsPermitted { get; init; }

    public string? RefusalCode { get; init; }

    public string? RefusalMessage { get; init; }

    /// <summary>The proration policy version that decided the figures.</summary>
    public string? PolicyVersion { get; init; }
}

/// <summary>
/// The next charge on a subscription.
/// <para>
/// <b>Served rather than forecast on the device, because the current plan price is not the
/// answer.</b> A promotion, a trial or a repricing policy all change it, and a client multiplying a
/// plan price by nothing at all would be right most months and wrong on exactly the ones somebody
/// notices.
/// </para>
/// </summary>
public sealed record UpcomingChargeResponse
{
    public required Guid SubscriptionId { get; init; }

    public required string PlanCode { get; init; }

    /// <summary>What will be charged, priced by the policy engine now.</summary>
    public required MoneyResponse Amount { get; init; }

    public required DateTimeOffset ChargeAt { get; init; }

    /// <summary>
    /// False when nothing will be charged — a cancellation is pending, or the plan is free. The
    /// amount is then zero and the date is when access ends instead.
    /// </summary>
    public required bool WillRenew { get; init; }

    public string? PolicyVersion { get; init; }
}
