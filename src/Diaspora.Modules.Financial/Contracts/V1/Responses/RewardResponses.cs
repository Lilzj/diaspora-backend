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
    public required long Balance { get; init; }

    public required long LifetimeEarned { get; init; }

    public required long LifetimeConverted { get; init; }

    /// <summary>
    /// False when this account may not convert. The reason is deliberately absent — a refusal that
    /// explains which check it tripped is a map for getting round it.
    /// </summary>
    public required bool CanConvert { get; init; }

    public required DateTimeOffset ResolvedAt { get; init; }
}

/// <summary>One movement of points, earned or converted away.</summary>
public sealed record RewardEntryResponse
{
    public required Guid EntryId { get; init; }

    /// <summary>Always positive. Which way it moved is <see cref="IsConversion"/>.</summary>
    public required long Points { get; init; }

    public required bool IsConversion { get; init; }

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
}
