using Diaspora.Modules.Creator.Domain.Earnings;
using Diaspora.Modules.Creator.Domain.Enums;

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

    /// <summary>
    /// The ledger transaction that settled it, where there was one.
    /// <para>
    /// Resolvable at <c>GET /financial/transactions/{transactionId}</c>, which is what lets a
    /// creator open an earning and see the money movement behind it. Null means there is nothing to
    /// open, and a client must render it as such rather than linking anyway.
    /// </para>
    /// </summary>
    public Guid? TransactionId { get; init; }

    /// <summary>When the money moved, not when the row was written.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? SettledAt { get; init; }

    /// <summary>Why it is not payable, when it is not.</summary>
    public string? StateReason { get; init; }

    /// <summary>
    /// When a pending earning clears. Null once it has.
    /// <para>
    /// Derived from the configured clearing period rather than left for the client to add days to a
    /// date it half-knows the rule for. "Available on the 5th" is a promise, and the platform that
    /// made the rule should be the one that states it.
    /// </para>
    /// </summary>
    public DateTimeOffset? AvailableAt { get; init; }

    /// <summary>
    /// What this earning was, in words.
    /// <para>
    /// Composed from the source and the subject rather than stored, so it cannot drift from the
    /// fields beside it — and served rather than composed on the device, so the wording is the
    /// platform's and translatable in one place.
    /// </para>
    /// </summary>
    public required string Description { get; init; }
}

/// <summary>
/// A page of earnings.
/// <para>
/// <b>A page rather than a capped list.</b> The first shape of this route answered the most recent
/// fifty and carried nothing to say there were more, which is a ledger with rows missing that a
/// creator cannot see are missing. On money, that is the worst version of the empty-filter defect.
/// </para>
/// </summary>
public sealed record CreatorEarningPageResponse
{
    public required IReadOnlyList<CreatorEarningResponse> Items { get; init; }

    /// <summary>Pass back as <c>cursor</c>. Null when this page is the last one.</summary>
    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}

/// <summary>One source's total on the revenue summary.</summary>
public sealed record CreatorRevenueSourceResponse
{
    public required string Source { get; init; }

    /// <summary>The source in words, for a screen that should not hold its own copy.</summary>
    public required string Label { get; init; }

    public required long MinorUnits { get; init; }

    /// <summary>How many earnings make up the total. A single £500 sponsorship is not five £100 ones.</summary>
    public required int Count { get; init; }

    /// <summary>
    /// This source's share of the total, as a percentage.
    /// <para>
    /// <b>Computed here, not on the device.</b> §11 forbids revenue being authoritative client-side
    /// data, and a client dividing one served figure by another is doing arithmetic on money whose
    /// rounding nobody agreed. The server holds both numbers; it can divide them.
    /// </para>
    /// </summary>
    public required decimal SharePercentage { get; init; }
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

    /// <summary>What was earned in the window this summary covers.</summary>
    public required long CurrentPeriodMinorUnits { get; init; }

    /// <summary>The window in words — "the last 30 days", or "all time" when unbounded.</summary>
    public required string CurrentPeriodLabel { get; init; }

    /// <summary>
    /// When these totals were computed.
    /// <para>
    /// A revenue figure with no timestamp cannot be told from a stale one, and a creator refreshing
    /// a screen that silently served a cached total would have no way to know.
    /// </para>
    /// </summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    public required IReadOnlyList<CreatorRevenueSourceResponse> BySource { get; init; }
}

public static class CreatorEarningMapper
{
    /// <summary>The source in words. One place, so a screen does not keep its own copy.</summary>
    public static string LabelFor(CreatorEarningSource source) => source switch
    {
        CreatorEarningSource.Donation => "Donations",
        CreatorEarningSource.SponsoredContent => "Sponsored content",
        CreatorEarningSource.Subscription => "Subscriptions",
        CreatorEarningSource.Referral => "Referrals",
        _ => source.ToString(),
    };

    private static string DescribeSubject(string? subjectType) => subjectType switch
    {
        "listing" => "a marketplace listing",
        "housing" => "a property",
        "job" => "a job",
        "event" => "an event",
        "guide" => "a guide",
        "donation" => "a donation",
        _ => "your work",
    };

    public static CreatorEarningResponse ToResponse(CreatorEarning earning, int clearingDays = 0)
    {
        ArgumentNullException.ThrowIfNull(earning);

        return new CreatorEarningResponse
        {
            Description = earning.Source == CreatorEarningSource.Donation
                ? "A donation"
                : $"Promotion of {DescribeSubject(earning.SubjectType)}",

            AvailableAt = earning.State == CreatorEarningState.Pending && clearingDays > 0
                ? earning.OccurredAt.AddDays(clearingDays)
                : null,

            EarningId = earning.Id.Value,
            Source = earning.Source.ToString(),
            State = earning.State.ToString(),
            AmountMinorUnits = earning.AmountMinorUnits,
            GrossMinorUnits = earning.GrossMinorUnits,
            Currency = earning.Currency,
            Exponent = earning.Exponent,
            SubjectType = earning.SubjectType,
            SubjectId = earning.SubjectId,
            TransactionId = earning.TransactionId,
            OccurredAt = earning.OccurredAt,
            SettledAt = earning.SettledAt,
            StateReason = earning.StateReason,
        };
    }
}
