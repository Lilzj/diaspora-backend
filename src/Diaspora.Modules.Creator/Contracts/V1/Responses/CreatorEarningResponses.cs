using Diaspora.Modules.Creator.Domain.Earnings;

namespace Diaspora.Modules.Creator.Contracts.V1.Responses;

/// <summary>
/// One earning, as a creator reads it.
/// <para>
/// <b>Both figures.</b> The share and the gross are on the wire together, so a creator asking "why
/// is this £5 when they paid £10" has the answer in front of them rather than a support ticket. A
/// response carrying only the net makes the platform's cut invisible, which is the opposite of what
/// a revenue screen is for.
/// </para>
/// </summary>
public sealed record CreatorEarningResponse
{
    public required Guid EarningId { get; init; }

    /// <summary>A <c>CreatorEarningSource</c> name.</summary>
    public required string Source { get; init; }

    /// <summary>A <c>CreatorEarningState</c> name.</summary>
    public required string State { get; init; }

    /// <summary>The creator's share, in minor units.</summary>
    public required long AmountMinorUnits { get; init; }

    /// <summary>What the member actually paid, in minor units.</summary>
    public required long GrossMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required int Exponent { get; init; }

    /// <summary>What the money was against. Null for a referral bonus.</summary>
    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    /// <summary>When the money moved, not when the row was written.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? SettledAt { get; init; }

    /// <summary>Why it is not payable, when it is not.</summary>
    public string? StateReason { get; init; }
}

/// <summary>One source's total on the revenue summary.</summary>
public sealed record CreatorRevenueSourceResponse
{
    public required string Source { get; init; }

    public required long MinorUnits { get; init; }

    /// <summary>How many earnings make up the total. A single £500 sponsorship is not five £100 ones.</summary>
    public required int Count { get; init; }
}

/// <summary>
/// What a creator has earned, totalled.
/// <para>
/// <b>Five figures rather than one, because they answer different questions.</b> "How much have I
/// earned" and "how much can I take out" are not the same, and a dashboard showing a lifetime total
/// where a member expects a balance is the failure this whole gap exists to fix.
/// </para>
/// <para>
/// <b><see cref="LifetimeMinorUnits"/> excludes what was taken back.</b> A creator reading "£400
/// earned" beside "£40 available" would otherwise be looking at £360 that was refunded.
/// <see cref="ReversedMinorUnits"/> carries that separately, so it is visible rather than netted
/// away.
/// </para>
/// </summary>
public sealed record CreatorRevenueSummaryResponse
{
    public required string Currency { get; init; }

    public required int Exponent { get; init; }

    /// <summary>Earned and not reversed: pending + available + processing + paid.</summary>
    public required long LifetimeMinorUnits { get; init; }

    /// <summary>Recorded, still inside its waiting period.</summary>
    public required long PendingMinorUnits { get; init; }

    /// <summary>Cleared, and countable towards a payout. The figure a creator reads twice.</summary>
    public required long AvailableMinorUnits { get; init; }

    /// <summary>Named on a payout that has not settled.</summary>
    public required long ProcessingMinorUnits { get; init; }

    public required long PaidMinorUnits { get; init; }

    /// <summary>Reversed, refunded or cancelled. Shown, not netted away.</summary>
    public required long ReversedMinorUnits { get; init; }

    /// <summary>How long an earning waits before it can be paid out.</summary>
    public required int ClearingDays { get; init; }

    /// <summary>
    /// Whether this creator has earnings in a currency these totals leave out.
    /// <para>
    /// Summing across currencies would produce a figure that is not money in any of them, so the
    /// summary answers in one and says plainly that it did.
    /// </para>
    /// </summary>
    public required bool HasOtherCurrencies { get; init; }

    public required IReadOnlyList<CreatorRevenueSourceResponse> BySource { get; init; }
}

public static class CreatorEarningMapper
{
    public static CreatorEarningResponse ToResponse(CreatorEarning earning)
    {
        ArgumentNullException.ThrowIfNull(earning);

        return new CreatorEarningResponse
        {
            EarningId = earning.Id.Value,
            Source = earning.Source.ToString(),
            State = earning.State.ToString(),
            AmountMinorUnits = earning.AmountMinorUnits,
            GrossMinorUnits = earning.GrossMinorUnits,
            Currency = earning.Currency,
            Exponent = earning.Exponent,
            SubjectType = earning.SubjectType,
            SubjectId = earning.SubjectId,
            OccurredAt = earning.OccurredAt,
            SettledAt = earning.SettledAt,
            StateReason = earning.StateReason,
        };
    }
}
