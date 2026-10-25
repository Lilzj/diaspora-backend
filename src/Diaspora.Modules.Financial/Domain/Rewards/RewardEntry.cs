using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Rewards;

/// <summary>
/// One movement of reward points, owned by the account.
/// <para>
/// <b>Append-only, like the ledger, and for the same reason.</b> "Why do I have 340 points" is a
/// question a member asks and a support agent has to answer; a balance that was simply overwritten
/// cannot answer it. There is no operation on an entry but reading it, and marking a pending grant
/// vested, which records when it became spendable and changes none of what it was.
/// </para>
/// <para>
/// <b><see cref="Reference"/> is the idempotency key on an earning.</b> It is the grantor's own
/// handle for the thing that was rewarded — a referral, a milestone, a challenge — and it is uniquely
/// indexed per account, because only the grantor can know whether two events describe one earning.
/// </para>
/// <para><see cref="EntryId"/> is caller-supplied. <c>ValueGeneratedNever()</c>.</para>
/// </summary>
public sealed class RewardEntry : ValueObject
{
    private RewardEntry(
        Guid entryId,
        long points,
        bool isConversion,
        bool isSpend,
        string source,
        string reference,
        DateTimeOffset occurredAt,
        Guid? grantedByUserId = null,
        string? reason = null,
        string? policyVersion = null,
        DateTimeOffset? vestsAt = null)
    {
        EntryId = entryId;
        Points = points;
        IsConversion = isConversion;
        IsSpend = isSpend;
        Source = source;
        Reference = reference;
        OccurredAt = occurredAt;
        GrantedByUserId = grantedByUserId;
        Reason = reason;
        PolicyVersion = policyVersion;
        VestsAt = vestsAt;
    }

    private RewardEntry() { }

    public Guid EntryId { get; private set; }

    /// <summary>Always positive. Which way it moved is <see cref="IsConversion"/>.</summary>
    public long Points { get; private set; }

    /// <summary>False for an earning, true for points spent becoming money.</summary>
    public bool IsConversion { get; private set; }

    /// <summary>
    /// True for points exchanged for something that is not money — a creator reward redemption.
    /// <para>
    /// <b>Added in Wave 6, additively, and deliberately as a second flag rather than by turning the
    /// pair into a kind.</b> Widening <see cref="IsConversion"/> into an enum would have been a
    /// breaking column change on a table that already holds every member's earning history; a new
    /// boolean defaults to false on every existing row, which is exactly what those rows mean.
    /// </para>
    /// <para>
    /// <b>Both flags are debits and neither is money.</b> <see cref="IsConversion"/> says the points
    /// became currency and went through the ledger; this says they bought an entitlement and did
    /// not. The Creator Platform never touches the second — it asks
    /// <c>IRewardPointsAccount.TrySpendAsync</c>, which is B15's own code, so the single points
    /// balance stays B15's.
    /// </para>
    /// </summary>
    public bool IsSpend { get; private set; }

    /// <summary>Whether this entry takes points away, for whichever of the two reasons.</summary>
    public bool IsDebit => IsConversion || IsSpend;

    /// <summary>Why. <c>ReferralQualified</c>, <c>GuidePublished</c>, <c>Conversion</c>.</summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>The grantor's own handle. Unique per account — see the class summary.</summary>
    public string Reference { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// The staff member who awarded these points by hand. Null for an earning a grantor's event
    /// recognised, and for every debit.
    /// <para>
    /// Added additively: every existing row is an earning or a debit, and null is what those rows
    /// mean. It is what the <c>reward.grant</c> policy's daily ceiling is counted against.
    /// </para>
    /// </summary>
    public Guid? GrantedByUserId { get; private set; }

    /// <summary>Why staff awarded these points. Required on a staff grant; null otherwise.</summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// The version of the rule that decided this earning: the grantor's, carried on its event, or
    /// <c>reward.grant@N</c> for a staff grant. Null where no versioned rule decided it, which is every
    /// row written before this column existed and every grantor that does not stamp one yet.
    /// </summary>
    public string? PolicyVersion { get; private set; }

    /// <summary>
    /// When a grant becomes spendable (D-9, RWD-07). Until then its points are pending: they count
    /// toward what was earned and can be reversed, but cannot be converted or spent.
    /// <para>
    /// Null on every debit, on a refunded spend (those points were already spendable), and on every
    /// grant written before vesting existed: those count as vested, so existing balances stay
    /// convertible (D-5).
    /// </para>
    /// </summary>
    public DateTimeOffset? VestsAt { get; private set; }

    /// <summary>When the vesting sweep made this grant spendable. Null until then, and on every
    /// entry that never vested because it was never pending.</summary>
    public DateTimeOffset? VestedAt { get; private set; }

    /// <summary>A grant whose points are not spendable yet.</summary>
    public bool IsPending => !IsDebit && VestsAt is not null && VestedAt is null;

    public static RewardEntry Earned(
        Guid entryId,
        long points,
        string source,
        string reference,
        DateTimeOffset occurredAt,
        Guid? grantedByUserId = null,
        string? reason = null,
        string? policyVersion = null,
        DateTimeOffset? vestsAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return new RewardEntry(
            entryId,
            points,
            isConversion: false,
            isSpend: false,
            string.IsNullOrWhiteSpace(source) ? "Unspecified" : source.Trim(),
            reference.Trim(),
            occurredAt,
            grantedByUserId,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            string.IsNullOrWhiteSpace(policyVersion) ? null : policyVersion.Trim(),
            vestsAt);
    }

    public static RewardEntry Converted(
        Guid entryId, long points, string reference, DateTimeOffset occurredAt) =>
        new(entryId, points, isConversion: true, isSpend: false, "Conversion", reference.Trim(),
            occurredAt);

    /// <summary>
    /// Points exchanged for something that is not money.
    /// <para>
    /// The reference is the spender's own handle — a creator reward redemption's identifier — and is
    /// uniquely indexed per account like every other, so a retried redemption debits once.
    /// </para>
    /// </summary>
    public static RewardEntry Spent(
        Guid entryId, long points, string source, string reference, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return new RewardEntry(
            entryId,
            points,
            isConversion: false,
            isSpend: true,
            string.IsNullOrWhiteSpace(source) ? "Redemption" : source.Trim(),
            reference.Trim(),
            occurredAt);
    }

    /// <summary>The account's half of vesting. Only <see cref="RewardAccount.VestDue"/> calls it.</summary>
    internal void MarkVested(DateTimeOffset at) => VestedAt = at;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EntryId;
        yield return Points;
        yield return IsConversion;
        yield return IsSpend;
        yield return Reference;
    }
}
