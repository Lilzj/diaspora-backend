namespace Diaspora.Modules.Financial.Extensibility.Entitlements;

/// <summary>
/// How much of one entitlement a member has used.
/// <para>
/// <b><see cref="AtLimitBehaviour"/> travels with the number because the client must not assume
/// it.</b> Some limits refuse, some queue, some charge for the overage, and a meter that showed
/// 15/15 without saying which would be telling a member their next listing will fail when in fact it
/// will cost them.
/// </para>
/// </summary>
public sealed record EntitlementUsage
{
    /// <summary>The entitlement this is about. <c>listings.concurrent</c>.</summary>
    public required string Key { get; init; }

    /// <summary>How many have been used in the period. Never a guess.</summary>
    public required long Consumed { get; init; }

    /// <summary>
    /// What happens when the limit is reached: <c>Refuse</c>, <c>Queue</c> or <c>Overage</c>. The
    /// vertical that enforces the limit is the only thing that knows.
    /// </summary>
    public required string AtLimitBehaviour { get; init; }

    /// <summary>When the counting window opened.</summary>
    public required DateTimeOffset PeriodStart { get; init; }

    /// <summary>When it closes. Null for a concurrent limit, which has no window.</summary>
    public DateTimeOffset? PeriodEnd { get; init; }
}

/// <summary>The three answers to "what happens at the limit", spelled once.</summary>
public static class AtLimitBehaviours
{
    /// <summary>The action is refused until something frees a slot.</summary>
    public const string Refuse = "Refuse";

    /// <summary>The action is accepted and waits.</summary>
    public const string Queue = "Queue";

    /// <summary>The action is accepted and charged for.</summary>
    public const string Overage = "Overage";
}
