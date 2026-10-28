namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// A member's points balance.
/// <para>
/// <b>There is no currency on this record, and that is the contract.</b> A point is a count of
/// something the platform chose to recognise; what it is worth is a policy read at conversion time.
/// A client that saw a currency here would be one line away from adding points to a wallet balance.
/// </para>
/// </summary>
public sealed record RewardBalanceResponse
{
    /// <summary>Points that can be converted or spent now: vested points only (Q3).</summary>
    public required long Balance { get; init; }

    /// <summary>
    /// Points granted but not vested yet (D-9). Not spendable, and not counted in
    /// <see cref="Balance"/>. Additive: zero for a client that never reads it.
    /// </summary>
    public long PendingPoints { get; init; }

    /// <summary>When the next pending grant vests. Null when nothing is pending.</summary>
    public DateTimeOffset? NextVestsAt { get; init; }

    public required long LifetimeEarned { get; init; }

    public required long LifetimeConverted { get; init; }

    /// <summary>
    /// Points spent on rewards, net of any refunded redemption. With the two above it accounts for
    /// the balance and what is pending: earned − converted − spent. The app used to hard-code this
    /// to zero, so its arithmetic stopped matching the balance after the first redemption.
    /// </summary>
    public long LifetimeSpent { get; init; }

    /// <summary>
    /// Pending points taken back because their grantor withdrew them (D-9). Additive:
    /// earned − reversed − converted − spent = balance + pending.
    /// </summary>
    public long LifetimeReversed { get; init; }

    /// <summary>
    /// False when this points account is held or empty. <b>Superseded</b> by
    /// <see cref="RewardConversionTermsResponse.Member"/>, which also covers the deployment, the
    /// financial account, the minimum and today's allowance, and says why — except for a hold, whose
    /// own reason stays with staff. Kept, unchanged in meaning, for clients that still read it.
    /// </summary>
    public required bool CanConvert { get; init; }

    public required DateTimeOffset ResolvedAt { get; init; }
}

/// <summary>One movement of points: earned, converted away, or spent on a reward.</summary>
public sealed record RewardEntryResponse
{
    public required Guid EntryId { get; init; }

    /// <summary>
    /// Always positive. Which way it moved is <see cref="IsConversion"/>, <see cref="IsSpend"/> or
    /// <see cref="IsReversal"/>: any one means the points left.
    /// </summary>
    public required long Points { get; init; }

    public required bool IsConversion { get; init; }

    /// <summary>
    /// True when the points were spent on a reward. Without it a redemption reached the client as
    /// a positive entry and rendered as points earned — the same figure with the opposite meaning.
    /// </summary>
    public bool IsSpend { get; init; }

    /// <summary>
    /// True when pending points were taken back because their grantor withdrew them (D-9). Like
    /// <see cref="IsSpend"/>, the points left, and a client that ignored it would show them earned.
    /// </summary>
    public bool IsReversal { get; init; }

    /// <summary>Why. <c>ReferralQualified</c>, <c>GuidePublished</c>, <c>Conversion</c>.</summary>
    public required string Source { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// What points are worth today, and how many may be converted.
/// <para>
/// <b>Served rather than computed, because a rate invented on a client is a client pricing money.</b>
/// Everything a conversion screen needs to do arithmetic is here, and the arithmetic it does is still
/// only a preview — the server prices the real one.
/// </para>
/// </summary>
public sealed record RewardConversionTermsResponse
{
    /// <summary>Whether this deployment converts points at all.</summary>
    public required bool IsEnabled { get; init; }

    public string? DisabledReason { get; init; }

    public required string Currency { get; init; }

    /// <summary>
    /// How many minor units make one of <see cref="Currency"/>: 2 for GBP, 0 for JPY, 3 for KWD.
    /// <para>
    /// Without it the app wrote the rate as <c>pointsPerMinorUnit × 100</c> points to one unit, which
    /// is right only for two-decimal currencies: a hundred times out for yen, ten times for dinars.
    /// </para>
    /// </summary>
    public required int Exponent { get; init; }

    /// <summary>"£", "₦". Null when the currency has none; show the code instead.</summary>
    public string? Symbol { get; init; }

    /// <summary>Minor units of credit one point produces.</summary>
    public required decimal MinorUnitsPerPoint { get; init; }

    public required long MinimumPoints { get; init; }

    /// <summary>The most that may be converted in one go. Null when the platform states none.</summary>
    public long? MaximumPoints { get; init; }

    /// <summary>The most in a rolling day, and what is left of it for this member.</summary>
    public long? DailyMaximumPoints { get; init; }

    public long? DailyRemainingPoints { get; init; }

    /// <summary>The policy version these terms came from. <c>reward.conversion@2</c>.</summary>
    public string? PolicyVersion { get; init; }

    /// <summary>
    /// Whether the caller may convert right now, and if not, why. The terms above are the platform's;
    /// this is the member's.
    /// </summary>
    public required RewardConversionMemberResponse Member { get; init; }
}

/// <summary>
/// Whether one member may convert right now.
/// <para>
/// The app worked eligibility out from <c>terms.isEnabled &amp;&amp; balance.canConvert</c>, and
/// <c>canConvert</c> is false below the minimum with no reason given, so a member short of the
/// minimum was told conversion was "not available on your account" and never saw the minimum.
/// </para>
/// </summary>
public sealed record RewardConversionMemberResponse
{
    public required bool CanConvert { get; init; }

    /// <summary>
    /// Why not, checked in this order: <c>financial.rewards.conversion_not_supported</c> or
    /// <c>currency_not_offered</c> (the deployment); <c>financial.account.not_eligible</c> or
    /// <c>restricted</c> (the financial account); <c>financial.rewards.not_eligible</c> (a hold);
    /// <c>financial.rewards.below_minimum</c>; <c>financial.rewards.daily_limit_reached</c>. Null when
    /// the member may convert.
    /// </summary>
    public string? ReasonCode { get; init; }

    /// <summary>Why not, as a sentence the member reads. A hold says only that it is a hold.</summary>
    public string? ReasonMessage { get; init; }

    /// <summary>
    /// The most the member may convert in one go right now: the smallest of the single-conversion
    /// maximum, what is left of today's allowance, and the balance. Null when they may not convert.
    /// </summary>
    public long? MaximumNow { get; init; }
}

/// <summary>
/// What a conversion would produce, priced by the server.
/// <para>
/// <b>Asked for rather than computed, even though the rate is on the terms.</b> The frontend's own
/// note says the preview must be a request so the flow is correct the day rounding changes; the
/// server is the only thing that may decide what a member gets.
/// </para>
/// </summary>
public sealed record RewardConversionPreviewResponse
{
    /// <summary>What was asked for.</summary>
    public required long Points { get; init; }

    /// <summary>
    /// What would leave the balance: the fewest points that buy <see cref="Credit"/>. Points that
    /// would only buy a fraction of a minor unit stay with the member.
    /// </summary>
    public long PointsConsumed { get; init; }

    public required MoneyResponse Credit { get; init; }

    public required decimal MinorUnitsPerPoint { get; init; }

    /// <summary>False when the platform would refuse this conversion, with a code saying why.</summary>
    public required bool IsPermitted { get; init; }

    public string? RefusalCode { get; init; }

    public string? RefusalMessage { get; init; }

    public string? PolicyVersion { get; init; }
}

/// <summary>One crossing from points into money, as it happened.</summary>
public sealed record RewardConversionResponse
{
    public required Guid ConversionId { get; init; }

    /// <summary>The points that left the balance.</summary>
    public required long Points { get; init; }

    /// <summary>
    /// What the member asked to convert, when this response is the conversion itself. Null on a
    /// replay or in history, where only what actually moved is recorded.
    /// </summary>
    public long? PointsRequested { get; init; }

    public required MoneyResponse Credited { get; init; }

    /// <summary>The rate as applied. Never recomputed from today's policy.</summary>
    public required decimal MinorUnitsPerPoint { get; init; }

    public string? PolicyVersion { get; init; }

    /// <summary>The ledger transaction the credit landed in. The audit handle.</summary>
    public Guid? TransactionId { get; init; }

    public required DateTimeOffset ConvertedAt { get; init; }

    /// <summary>
    /// <c>Holding</c> while the credit is pending, <c>Available</c> once it can be spent or withdrawn
    /// (D-7). A conversion made before the hold existed is <c>Available</c>.
    /// </summary>
    public string Status { get; init; } = "Available";

    /// <summary>When the credit becomes available. Null on a conversion made before the hold existed.</summary>
    public DateTimeOffset? AvailableAt { get; init; }
}

/// <summary>
/// Whether a member's points are held from conversion, and every decision about it. Staff only.
/// <para>
/// The reasons here are operators' notes. A held member sees only that conversion is held — the
/// generic message the terms and the preview carry — never which check tripped.
/// </para>
/// </summary>
public sealed record RewardConversionHoldResponse
{
    public required Guid UserId { get; init; }

    public required bool IsHeld { get; init; }

    /// <summary>The note on the hold in force. Null when the member is not held.</summary>
    public string? Reason { get; init; }

    /// <summary>Every hold placed and lifted, newest first.</summary>
    public required IReadOnlyList<RewardHoldDecisionResponse> Decisions { get; init; }
}

/// <summary>One hold placed or lifted: who decided, when, and why.</summary>
public sealed record RewardHoldDecisionResponse
{
    /// <summary>True for a hold placed, false for a hold lifted.</summary>
    public required bool IsHeld { get; init; }

    public required string Reason { get; init; }

    /// <summary>The staff member who decided. Null for a decision the platform made itself.</summary>
    public Guid? DecidedByUserId { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}

/// <summary>One points account held from conversion, as the staff review queue shows it.</summary>
public sealed record RewardHoldResponse
{
    public required Guid UserId { get; init; }

    /// <summary>Vested points, which the hold keeps from conversion.</summary>
    public required long Balance { get; init; }

    /// <summary>Points still vesting. They vest under a hold; they do not convert.</summary>
    public required long PendingBalance { get; init; }

    /// <summary>
    /// The decision in force: the latest hold placed. Its <c>DecidedByUserId</c> is null when the
    /// platform placed it itself.
    /// </summary>
    public required RewardHoldDecisionResponse LatestDecision { get; init; }
}

/// <summary>A staff grant above the threshold, asked for and waiting for, or decided by, a second Admin.</summary>
public sealed record RewardGrantRequestResponse
{
    public required Guid RequestId { get; init; }

    public required Guid MemberUserId { get; init; }

    public required long Points { get; init; }

    public required string Source { get; init; }

    public required string GrantReference { get; init; }

    public required string Reason { get; init; }

    /// <summary><c>Pending</c>, <c>Approved</c>, <c>Rejected</c> or <c>Withdrawn</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The Admin who asked. The grant is theirs and counts toward their day.</summary>
    public required Guid RequestedByUserId { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>Who approved or rejected it. Null while pending and on a withdrawal.</summary>
    public Guid? DecidedByUserId { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    public string? DecisionNote { get; init; }

    /// <summary>The <c>reward.grant</c> version that allowed the approved grant.</summary>
    public string? PolicyVersion { get; init; }
}

/// <summary>What one currency's rewards pot can pay for conversions. Staff only.</summary>
public sealed record RewardPotResponse
{
    public required string Currency { get; init; }

    public required MoneyResponse Available { get; init; }
}
