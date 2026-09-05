using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Domain.Earnings;

/// <summary>
/// One sum a creator earned, and what it was for.
/// <para>
/// <b>This is the attribution B15 cannot give.</b> A transaction says "+£12.00 credit" and stops.
/// It cannot say the twelve pounds was a sponsorship share rather than a refund, and the revenue
/// dashboard's whole job is to say exactly that. CREATOR-GAP-008 records the consequence: the wallet
/// balance on that screen is real and every breakdown beside it was simulated, because there was
/// nothing on the platform that knew what a movement <em>meant</em>.
/// </para>
/// <para>
/// <b>An earning is not money, and this aggregate never moves any.</b> The wallet is B15's. What is
/// recorded here is a claim — "this much, from this, in this state" — and a payout is what turns a
/// claim into a balance. Keeping the two apart is what lets a creator be shown £120 earned and
/// £40 available without either figure being a lie.
/// </para>
/// <para>
/// <b><see cref="SourceReference"/> is the idempotency barrier</b>, exactly as on
/// <c>CreatorActivityEntry</c>. Delivery is at-least-once, so a redelivered
/// <c>DonationCompletedIntegrationEvent</c> must not pay twice. The reference is the producing
/// event's own subject — <c>donation.completed:{donationId}</c> — and it is uniquely indexed, so two
/// workers racing one delivery lose one at the database rather than both recording an earning.
/// </para>
/// <para>
/// <b>Money is stored as primitives</b> (<see cref="AmountMinorUnits"/>, <see cref="Currency"/>,
/// <see cref="Exponent"/>) rather than as a value object, matching every other money-bearing
/// aggregate in the platform. A composite is exposed for reading and ignored by the mapping.
/// </para>
/// </summary>
public sealed class CreatorEarning : AggregateRoot<CreatorEarningId>
{
    private CreatorEarning(
        CreatorEarningId id,
        CreatorId creatorId,
        Guid userId,
        CreatorEarningSource source,
        long amountMinorUnits,
        long grossMinorUnits,
        string currency,
        int exponent,
        string sourceReference,
        string? subjectType,
        Guid? subjectId,
        DateTimeOffset occurredAt) : base(id)
    {
        CreatorId = creatorId;
        UserId = userId;
        Source = source;
        AmountMinorUnits = amountMinorUnits;
        GrossMinorUnits = grossMinorUnits;
        Currency = currency;
        Exponent = exponent;
        SourceReference = sourceReference;
        SubjectType = subjectType;
        SubjectId = subjectId;
        OccurredAt = occurredAt;
        State = CreatorEarningState.Pending;
    }

    private CreatorEarning() { }

    public CreatorId CreatorId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public CreatorEarningSource Source { get; private set; }

    public CreatorEarningState State { get; private set; }

    /// <summary>The creator's share, after the configured rate.</summary>
    public long AmountMinorUnits { get; private set; }

    /// <summary>
    /// What the member actually paid.
    /// <para>
    /// Kept alongside the share so a creator can see both figures. A dashboard showing only the net
    /// invites "why is this £5 when they paid £10", and the honest answer is a rate somebody set —
    /// which cannot be shown if the gross was thrown away at the boundary.
    /// </para>
    /// </summary>
    public long GrossMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; }

    /// <summary>The producing event's own subject. Uniquely indexed.</summary>
    public string SourceReference { get; private set; } = string.Empty;

    /// <summary>What the money was against — a listing, a guide. Null for a referral bonus.</summary>
    public string? SubjectType { get; private set; }

    public Guid? SubjectId { get; private set; }

    /// <summary>When the money moved, not when this row was written.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Set when a payout names this earning.</summary>
    public Guid? PayoutId { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    /// <summary>Why it is not payable, when it is not. Shown to the creator.</summary>
    public string? StateReason { get; private set; }

    public int Revision { get; private set; }

    public static Result<CreatorEarning> Record(
        CreatorId creatorId,
        Guid userId,
        CreatorEarningSource source,
        long amountMinorUnits,
        long grossMinorUnits,
        string? currency,
        int exponent,
        string? sourceReference,
        string? subjectType,
        Guid? subjectId,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(sourceReference))
        {
            return Result.Failure<CreatorEarning>(CreatorEarningErrors.SourceReferenceRequired);
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result.Failure<CreatorEarning>(CreatorEarningErrors.CurrencyRequired);
        }

        // A zero share is a legitimate outcome — a source whose rate is set to nothing — and it is
        // recorded rather than dropped, so the dashboard can show that something was attributed and
        // carried no share. A *negative* share is arithmetic nobody asked for.
        if (amountMinorUnits < 0 || grossMinorUnits < 0)
        {
            return Result.Failure<CreatorEarning>(CreatorEarningErrors.AmountNegative);
        }

        if (amountMinorUnits > grossMinorUnits)
        {
            return Result.Failure<CreatorEarning>(CreatorEarningErrors.ShareExceedsGross);
        }

        return Result.Success(new CreatorEarning(
            CreatorEarningId.New(),
            creatorId,
            userId,
            source,
            amountMinorUnits,
            grossMinorUnits,
            currency.Trim().ToUpperInvariant(),
            exponent,
            sourceReference.Trim(),
            subjectType,
            subjectId,
            occurredAt));
    }

    /// <summary>Clears the earning for payout.</summary>
    public Result MakeAvailable(DateTimeOffset now)
    {
        if (State != CreatorEarningState.Pending)
        {
            return Result.Failure(CreatorEarningErrors.NotPending);
        }

        State = CreatorEarningState.Available;
        StateReason = null;
        Revision++;
        return Result.Success();
    }

    /// <summary>Names this earning on a payout.</summary>
    public Result AttachToPayout(Guid payoutId)
    {
        if (State != CreatorEarningState.Available)
        {
            return Result.Failure(CreatorEarningErrors.NotAvailable);
        }

        PayoutId = payoutId;
        State = CreatorEarningState.Processing;
        Revision++;
        return Result.Success();
    }

    /// <summary>The payout settled.</summary>
    public Result MarkPaid(DateTimeOffset settledAt)
    {
        if (State != CreatorEarningState.Processing)
        {
            return Result.Failure(CreatorEarningErrors.NotProcessing);
        }

        State = CreatorEarningState.Paid;
        SettledAt = settledAt;
        Revision++;
        return Result.Success();
    }

    /// <summary>
    /// The originating payment came back.
    /// <para>
    /// Terminal, and deliberately allowed from any state that is not already terminal: a refund can
    /// land after a payout has settled, and refusing to record it would leave the dashboard showing
    /// an earning the platform knows is gone.
    /// </para>
    /// </summary>
    public Result Reverse(CreatorEarningState terminal, string? reason)
    {
        if (terminal is not (CreatorEarningState.Reversed
            or CreatorEarningState.Refunded
            or CreatorEarningState.Cancelled))
        {
            return Result.Failure(CreatorEarningErrors.NotATerminalReversal);
        }

        State = terminal;
        StateReason = reason;
        Revision++;
        return Result.Success();
    }
}
