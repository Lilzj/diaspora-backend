namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// What a member may move, and what it costs them.
/// <para>
/// <b>Served so the client never invents one of these numbers.</b> A minimum, a maximum, a daily
/// ceiling and a fee are exactly the four figures a client would otherwise hard-code, and the four a
/// hostile client would most like to choose. Every one of them comes from a versioned policy row.
/// </para>
/// <para>
/// <b>A null is "no stated limit", never zero.</b> A client renders a ceiling where there is one and
/// says nothing where there is not, rather than drawing a bar at zero that reads as a refusal.
/// </para>
/// </summary>
public sealed record MovementLimitsResponse
{
    /// <summary>Whether this deployment offers the operation at all.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Why not, when it is not. A code the client can branch on.</summary>
    public string? DisabledReason { get; init; }

    public required string Currency { get; init; }

    public MoneyResponse? Minimum { get; init; }

    public MoneyResponse? Maximum { get; init; }

    /// <summary>The most that may move in a rolling day.</summary>
    public MoneyResponse? DailyMaximum { get; init; }

    /// <summary>
    /// What is left of today's allowance for this member. Null when no daily ceiling is stated.
    /// </summary>
    public MoneyResponse? DailyRemaining { get; init; }

    /// <summary>A fixed charge on the operation. Absent when the deployment charges none.</summary>
    public MoneyResponse? FlatFee { get; init; }

    /// <summary>A proportion of the amount, as a percentage. 1.5 means one and a half per cent.</summary>
    public decimal? FeePercentage { get; init; }

    /// <summary>What the member can spend right now, for a withdrawal. Their available balance.</summary>
    public MoneyResponse? Available { get; init; }
}
