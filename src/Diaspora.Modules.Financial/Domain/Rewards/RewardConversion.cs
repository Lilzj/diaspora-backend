using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Rewards;

/// <summary>
/// One crossing from points into money. Its amounts and rate are recorded once and never change.
/// <para>
/// <b>The rate that was applied is stored, not the rate that applies today.</b> This is the same rule
/// that puts a policy version on every ledger entry, and it matters more here than almost anywhere
/// else: a conversion recomputed from a later rate would produce a plausible, wrong figure with
/// nothing reporting a problem. The policy version travels beside the rate so a support agent can see
/// which rule decided it.
/// </para>
/// <para>
/// <b>The transaction it produced is named here.</b> That is what lets somebody walk from "I
/// converted 500 points" to the ledger entries that credited the wallet, and back.
/// </para>
/// <para>
/// <b>The credit is held before it can be used</b> (D-7). It lands in the wallet's pending bucket and
/// <see cref="AvailableAt"/> says when it may move to available; <see cref="ReleasedAt"/> records that
/// it did. Those two dates are the only thing about a conversion that changes.
/// </para>
/// </summary>
public sealed class RewardConversion : AggregateRoot<RewardConversionId>, IAuditableEntity
{
    private RewardConversion(
        RewardConversionId id,
        Guid memberUserId,
        long points,
        Money credited,
        decimal rateMinorUnitsPerPoint,
        WalletId walletId,
        DateTimeOffset convertedAt) : base(id)
    {
        MemberUserId = memberUserId;
        Points = points;
        CreditedMinorUnits = credited.MinorUnits;
        Currency = credited.Currency;
        Exponent = credited.Exponent;
        RateMinorUnitsPerPoint = rateMinorUnitsPerPoint;
        WalletId = walletId;
        ConvertedAt = convertedAt;
        CreatedAt = convertedAt;
    }

    private RewardConversion() { }

    public Guid MemberUserId { get; private set; }

    public long Points { get; private set; }

    public long CreditedMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>Minor units per point, as applied. Never recomputed.</summary>
    public decimal RateMinorUnitsPerPoint { get; private set; }

    /// <summary>The policy version that decided the rate. <c>reward.conversion@2</c>.</summary>
    public string? PolicyVersion { get; private set; }

    public WalletId WalletId { get; private set; } = default!;

    /// <summary>The ledger transaction the credit landed in.</summary>
    public TransactionId? TransactionId { get; private set; }

    /// <summary>Unique when present. What makes a double-tapped convert button one conversion.</summary>
    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset ConvertedAt { get; private set; }

    /// <summary>
    /// When the credit may leave the pending bucket. Null on a conversion made before the hold
    /// existed, whose credit landed available at once.
    /// </summary>
    public DateTimeOffset? AvailableAt { get; private set; }

    /// <summary>When the release posting moved the credit to available. Null while it is held.</summary>
    public DateTimeOffset? ReleasedAt { get; private set; }

    /// <summary>Whether the credit is still in the pending bucket.</summary>
    public bool IsHolding => AvailableAt is not null && ReleasedAt is null;

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Credited => Money.FromMinorUnits(CreditedMinorUnits, Currency, Exponent);

    public static RewardConversion Record(
        RewardConversionId id,
        Guid memberUserId,
        long points,
        Money credited,
        decimal rateMinorUnitsPerPoint,
        WalletId walletId,
        DateTimeOffset convertedAt,
        string? policyVersion = null,
        string? idempotencyKey = null,
        TransactionId? transactionId = null,
        DateTimeOffset? availableAt = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(credited);
        ArgumentNullException.ThrowIfNull(walletId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);

        if (credited.IsZero)
        {
            throw new DomainException(
                "A conversion that produces no credit takes points for nothing.",
                FinancialErrorCodes.RewardBelowMinimum);
        }

        return new RewardConversion(
            id, memberUserId, points, credited, rateMinorUnitsPerPoint, walletId, convertedAt)
        {
            PolicyVersion = string.IsNullOrWhiteSpace(policyVersion) ? null : policyVersion.Trim(),
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            TransactionId = transactionId,
            AvailableAt = availableAt
        };
    }

    /// <summary>
    /// Records that the hold ended and the credit moved to available. False, and nothing changes,
    /// when there is no hold or it was already released, so a second sweep cannot release twice.
    /// </summary>
    public bool Release(DateTimeOffset at)
    {
        if (!IsHolding)
        {
            return false;
        }

        ReleasedAt = at;
        Revision++;

        return true;
    }

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
}
