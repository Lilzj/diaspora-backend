using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Rewards;

/// <summary>
/// One member's reward points. Points are not money.
/// <para>
/// <b>This is deliberately not a wallet, and none of the vocabulary is shared.</b> There is no
/// currency, no exponent, no <c>Money</c> anywhere on this type and no ledger entry behind it —
/// because a point is a count of something the platform chose to recognise, not a claim on the
/// platform's balance sheet. Making points a wallet balance would mean every reporting surface that
/// sums balances silently included them, and the platform would be carrying a liability it never
/// decided to take on.
/// </para>
/// <para>
/// <b>The crossing is one operation, in one place.</b> <see cref="Convert"/> is the only member that
/// reduces a balance, and the only caller that may use it is the conversion command — which posts a
/// real ledger credit at a policy-versioned rate in the same unit of work. Everything upstream counts
/// points; everything downstream counts minor units; the boundary is a single method.
/// </para>
/// <para>
/// <b>Every movement is an entry, like the ledger.</b> The balance is a projection of
/// <see cref="Entries"/>, so "why do I have 340 points" is answerable and a disputed grant is
/// visible rather than inferred from a number that changed.
/// </para>
/// </summary>
public sealed class RewardAccount : AggregateRoot<RewardAccountId>, IAuditableEntity
{
    private readonly List<RewardEntry> _entries = [];

    private RewardAccount(RewardAccountId id, Guid memberUserId, DateTimeOffset createdAt) : base(id)
    {
        MemberUserId = memberUserId;
        CreatedAt = createdAt;
    }

    private RewardAccount() { }

    public Guid MemberUserId { get; private set; }

    /// <summary>Points available to convert. Never negative.</summary>
    public long Balance { get; private set; }

    /// <summary>Everything ever earned. Never decreases — the number a member's profile shows.</summary>
    public long LifetimeEarned { get; private set; }

    /// <summary>Everything ever converted away.</summary>
    public long LifetimeConverted { get; private set; }

    /// <summary>
    /// Everything ever exchanged for something that is not money.
    /// <para>
    /// Added in Wave 6 alongside <see cref="Spend"/>. Kept apart from
    /// <see cref="LifetimeConverted"/> because "what has this member turned into currency" and "what
    /// have they redeemed" are two different questions, and a single figure would answer neither.
    /// </para>
    /// </summary>
    public long LifetimeSpent { get; private set; }

    /// <summary>
    /// True when this member may not convert — a fraud hold, an unresolved dispute.
    /// <para>
    /// The refusal it produces is deliberately vague to the member. A block that explains which check
    /// it tripped is a map for getting round it.
    /// </para>
    /// </summary>
    public bool IsConversionBlocked { get; private set; }

    public string? BlockReason { get; private set; }

    public IReadOnlyList<RewardEntry> Entries => _entries.AsReadOnly();

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static RewardAccount Open(RewardAccountId id, Guid memberUserId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new RewardAccount(id, memberUserId, createdAt);
    }

    /// <summary>
    /// Records points earned.
    /// <para>
    /// <b>The grant reference is the anti-double-award barrier and it is checked here as well as at
    /// the database.</b> A redelivered integration event carrying one reference awards once; the
    /// in-aggregate check catches a redelivery inside a single unit of work, and the unique index
    /// catches one across two.
    /// </para>
    /// </summary>
    public bool Grant(
        Guid entryId, long points, string source, string grantReference, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantReference);

        var reference = grantReference.Trim();

        if (_entries.Exists(entry =>
            string.Equals(entry.Reference, reference, StringComparison.Ordinal)))
        {
            return false;
        }

        _entries.Add(RewardEntry.Earned(entryId, points, source, reference, now));
        Balance += points;
        LifetimeEarned += points;
        Touch(now);

        return true;
    }

    /// <summary>
    /// Takes points out because they became money.
    /// <para>
    /// The only member that reduces a balance, and it refuses rather than clamping — a conversion the
    /// member cannot afford must fail loudly, because the alternative is a balance that quietly
    /// becomes zero and credit that was never backed by anything.
    /// </para>
    /// </summary>
    public void Convert(Guid entryId, long points, string conversionReference, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);

        if (IsConversionBlocked)
        {
            throw new DomainException(
                "Reward conversion is not available on this account at the moment.",
                FinancialErrorCodes.RewardNotEligible);
        }

        if (points > Balance)
        {
            throw new DomainException(
                "That is more points than this account holds.",
                FinancialErrorCodes.RewardInsufficientPoints);
        }

        _entries.Add(RewardEntry.Converted(entryId, points, conversionReference, now));
        Balance -= points;
        LifetimeConverted += points;
        Touch(now);
    }

    /// <summary>
    /// Takes points out because they bought something that is not money.
    /// <para>
    /// <b>The seam a creator platform redeems through, added in Wave 6.</b> A reward redemption has
    /// to reduce a balance or points are infinite, and the balance is this account's — so the debit
    /// happens here, inside B15, in the caller's own unit of work. The alternative considered and
    /// rejected was a second points ledger inside the Creator Platform, which the phase brief
    /// forbids outright and which would have made "how many points do I have" two questions.
    /// </para>
    /// <para>
    /// <b>Refuses rather than clamping, like <see cref="Convert"/>.</b> A redemption the member
    /// cannot afford must fail loudly, because the alternative is a balance that quietly becomes
    /// zero and an entitlement that was never paid for.
    /// </para>
    /// <para>
    /// <b>Idempotent on the reference, like <see cref="Grant"/>.</b> A retried redemption carrying
    /// the same handle debits once and answers false the second time, so the caller can tell a
    /// replay from a first spend without a second round trip.
    /// </para>
    /// </summary>
    /// <returns>True when this call debited. False when the reference had already been spent.</returns>
    public bool Spend(
        Guid entryId, long points, string source, string spendReference, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        ArgumentException.ThrowIfNullOrWhiteSpace(spendReference);

        var reference = spendReference.Trim();

        if (_entries.Exists(entry =>
            string.Equals(entry.Reference, reference, StringComparison.Ordinal)))
        {
            return false;
        }

        if (IsConversionBlocked)
        {
            throw new DomainException(
                "Reward redemption is not available on this account at the moment.",
                FinancialErrorCodes.RewardNotEligible);
        }

        if (points > Balance)
        {
            throw new DomainException(
                "That is more points than this account holds.",
                FinancialErrorCodes.RewardInsufficientPoints);
        }

        _entries.Add(RewardEntry.Spent(entryId, points, source, reference, now));
        Balance -= points;
        LifetimeSpent += points;
        Touch(now);

        return true;
    }

    /// <summary>
    /// Puts spent points back, because the platform could not deliver what they bought.
    /// <para>
    /// <b>An entry rather than an edit</b> — the ledger's own rule, applied here: the spend stays on
    /// the record and a compensating credit is appended beside it, so "why did my balance go down
    /// and up again" is answerable.
    /// </para>
    /// </summary>
    public bool RefundSpend(
        Guid entryId, long points, string source, string refundReference, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        ArgumentException.ThrowIfNullOrWhiteSpace(refundReference);

        var reference = refundReference.Trim();

        if (_entries.Exists(entry =>
            string.Equals(entry.Reference, reference, StringComparison.Ordinal)))
        {
            return false;
        }

        _entries.Add(RewardEntry.Earned(entryId, points, source, reference, now));
        Balance += points;
        LifetimeSpent = Math.Max(0, LifetimeSpent - points);
        Touch(now);

        return true;
    }

    /// <summary>Stops conversion without touching the balance. A hold, not a forfeiture.</summary>
    public void BlockConversion(string reason, DateTimeOffset now)
    {
        IsConversionBlocked = true;
        BlockReason = string.IsNullOrWhiteSpace(reason) ? "Blocked." : reason.Trim();
        Touch(now);
    }

    public void AllowConversion(DateTimeOffset now)
    {
        IsConversionBlocked = false;
        BlockReason = null;
        Touch(now);
    }

    /// <summary>How many points this account converted since a moment. The daily-cap input.</summary>
    public long ConvertedSince(DateTimeOffset since) =>
        _entries
            .Where(entry => entry.IsConversion && entry.OccurredAt >= since)
            .Sum(entry => entry.Points);

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
