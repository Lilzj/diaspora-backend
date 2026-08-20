using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>
/// A standing instruction to give the same amount to the same cause again.
/// <para>
/// <b>COMMUNITY-LIFE-GAP-008: the field existed and nothing charged against it.</b>
/// <c>Donation.RecurringIntervalMonths</c> has been on the certified request since B15 shipped,
/// documented as "declared and not scheduled", and the client's confirmation screen says so in
/// words. This aggregate is what turns that declaration into a schedule — and it is a separate row
/// rather than a re-read of the first donation, because a schedule has state the donation does not:
/// when it next runs, whether the last attempt failed, how many times, and whether the donor
/// stopped it.
/// </para>
/// <para>
/// <b>The claim is what makes two workers safe, and it is a database write rather than a lock.</b>
/// <see cref="TryClaim"/> moves <see cref="NextChargeAt"/> forward and stamps
/// <see cref="ClaimedAt"/> before any money is asked for, and <see cref="Revision"/> is the
/// concurrency token — so two instances that both loaded the same due schedule race on
/// <c>SaveChanges</c> and exactly one wins. A distributed lease over the sweep is the outer
/// protection; this is the one that still holds when the lease expires mid-run, which is the case a
/// lease alone cannot cover.
/// </para>
/// <para>
/// <b>Nothing here takes money.</b> The worker builds an ordinary donation through the ordinary
/// donation path — the same command a member's own tap goes through — so the amount, the currency,
/// the cause, the receipt and the payment intent are all produced by the code that already owns
/// them. A worker that marked a donation paid would be a second settlement authority.
/// </para>
/// </summary>
public sealed class RecurringDonation : AggregateRoot<RecurringDonationId>, IAuditableEntity
{
    /// <summary>How many consecutive failures before the schedule stops trying on its own.</summary>
    public const int MaxConsecutiveFailures = 4;

    private RecurringDonation(
        RecurringDonationId id,
        Guid donorUserId,
        DonationCauseId causeId,
        string causeCode,
        Money amount,
        int intervalMonths,
        DateTimeOffset nextChargeAt,
        DateTimeOffset createdAt) : base(id)
    {
        DonorUserId = donorUserId;
        CauseId = causeId;
        CauseCode = causeCode;
        AmountMinorUnits = amount.MinorUnits;
        Currency = amount.Currency;
        Exponent = amount.Exponent;
        IntervalMonths = intervalMonths;
        NextChargeAt = nextChargeAt;
        Status = RecurringDonationStatus.Active;
        CreatedAt = createdAt;
    }

    private RecurringDonation() { }

    /// <summary>
    /// Who gives. Never null.
    /// <para>
    /// <b>An anonymous donation cannot recur, and that is a consequence of anonymity rather than an
    /// omission.</b> B15 stores no donor identifier on an anonymous donation, so there would be
    /// nobody to charge next month and nobody to tell when the charge failed. The create path
    /// refuses the combination rather than storing an identifier it promised not to.
    /// </para>
    /// </summary>
    public Guid DonorUserId { get; private set; }

    public DonationCauseId CauseId { get; private set; } = default!;

    /// <summary>Denormalised so a schedule reads without a join, exactly as a donation carries it.</summary>
    public string CauseCode { get; private set; } = string.Empty;

    public long AmountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>Months between charges. One to twelve; the client offers one, three, six and twelve.</summary>
    public int IntervalMonths { get; private set; }

    public RecurringDonationStatus Status { get; private set; }

    /// <summary>
    /// When it runs next. Null once it is terminal — there is no next charge for a cancelled
    /// schedule, and a date sitting there would be a promise nothing intends to keep.
    /// </summary>
    public DateTimeOffset? NextChargeAt { get; private set; }

    /// <summary>When a worker last took this schedule. Diagnostic; the claim itself is the moved date.</summary>
    public DateTimeOffset? ClaimedAt { get; private set; }

    /// <summary>When money last actually moved.</summary>
    public DateTimeOffset? LastChargeAt { get; private set; }

    /// <summary>The donation the last successful charge produced.</summary>
    public DonationId? LastDonationId { get; private set; }

    /// <summary>When a charge was last attempted, whether or not it worked.</summary>
    public DateTimeOffset? LastAttemptAt { get; private set; }

    public string? LastFailureReason { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    /// <summary>How many charges have succeeded. The occurrence number on the event.</summary>
    public int ChargeCount { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>The donation that started this schedule. The audit trail back to the member's tap.</summary>
    public DonationId? OriginDonationId { get; private set; }

    /// <summary>B15's idempotency primitive, for the creating request. Uniquely indexed.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token, and the claim's guarantee.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency, Exponent);

    public bool IsTerminal =>
        Status is RecurringDonationStatus.Cancelled or RecurringDonationStatus.Completed;

    /// <summary>Whether a worker should pick this up as at the given moment.</summary>
    public bool IsDue(DateTimeOffset asOf) =>
        Status is RecurringDonationStatus.Active or RecurringDonationStatus.Retrying
        && NextChargeAt is { } next
        && next <= asOf;

    public static Result<RecurringDonation> Schedule(
        RecurringDonationId id,
        Guid donorUserId,
        DonationCause cause,
        Money amount,
        int intervalMonths,
        DateTimeOffset now,
        DonationId? originDonationId = null,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(cause);
        ArgumentNullException.ThrowIfNull(amount);

        if (donorUserId == Guid.Empty)
        {
            return Result.Failure<RecurringDonation>(Error.Validation(
                FinancialErrorCodes.RecurringDonationIntervalInvalid,
                "An anonymous donation cannot recur: there would be nobody to charge."));
        }

        if (intervalMonths is < 1 or > 12)
        {
            return Result.Failure<RecurringDonation>(Error.Validation(
                FinancialErrorCodes.RecurringDonationIntervalInvalid,
                "A recurring donation repeats between one and twelve months apart."));
        }

        if (amount.IsZero)
        {
            return Result.Failure<RecurringDonation>(Error.Validation(
                FinancialErrorCodes.AmountInvalid,
                "A recurring donation of nothing is not a donation."));
        }

        if (!string.Equals(amount.Currency, cause.Currency, StringComparison.Ordinal))
        {
            return Result.Failure<RecurringDonation>(Error.Validation(
                FinancialErrorCodes.CurrencyMismatch,
                $"A {amount.Currency} donation cannot recur against a {cause.Currency} cause."));
        }

        // The first charge is the one the member just made. The schedule starts one interval out.
        return Result.Success(new RecurringDonation(
            id, donorUserId, cause.Id, cause.Code, amount, intervalMonths,
            now.AddMonths(intervalMonths), now)
        {
            OriginDonationId = originDonationId,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()
        });
    }

    /// <summary>
    /// Takes the schedule for this cycle, moving the due date forward before anything is charged.
    /// <para>
    /// <b>Forward first, charge second, and that order is the whole safety property.</b> A worker
    /// that charged and then moved the date would charge twice if it died in between; one that moves
    /// the date first will at worst skip a cycle, and a missed donation is recoverable while a
    /// double charge on somebody's card is not.
    /// </para>
    /// <para>
    /// Refuses when the schedule is not due, which is what a second worker arriving after the first
    /// committed will find.
    /// </para>
    /// </summary>
    public bool TryClaim(DateTimeOffset now)
    {
        if (!IsDue(now))
        {
            return false;
        }

        // From the scheduled instant rather than from now, so a sweep that ran late does not walk
        // the anniversary forward month by month.
        var anchor = NextChargeAt ?? now;

        ClaimedAt = now;
        LastAttemptAt = now;
        NextChargeAt = anchor.AddMonths(IntervalMonths);
        Revision++;
        ModifiedAt = now;

        return true;
    }

    /// <summary>Records that a claimed cycle took its money.</summary>
    public void RecordCharge(DonationId donationId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(donationId);

        LastDonationId = donationId;
        LastChargeAt = now;
        LastFailureReason = null;
        ConsecutiveFailures = 0;
        ChargeCount++;
        Status = RecurringDonationStatus.Active;
        Revision++;
        ModifiedAt = now;

        RaiseDomainEvent(new RecurringDonationChargedDomainEvent
        {
            ScheduleId = Id,
            DonorUserId = DonorUserId,
            DonationId = donationId,
            CauseCode = CauseCode,
            AmountMinorUnits = AmountMinorUnits,
            Currency = Currency,
            OccurrenceNumber = ChargeCount,
            NextChargeAt = NextChargeAt,
            ChargedAt = now
        });
    }

    /// <summary>
    /// Records that a claimed cycle failed, and decides whether there will be another.
    /// <para>
    /// <b>A retry is sooner than the next cycle, and the schedule says when.</b> Waiting a whole
    /// month after one decline would lose the donation; retrying immediately would hit the same
    /// declined card. The retry is set a fixed number of days out, and after
    /// <see cref="MaxConsecutiveFailures"/> the schedule suspends itself and stops asking.
    /// </para>
    /// </summary>
    public void RecordFailure(string reason, int retryDays, DateTimeOffset now)
    {
        ConsecutiveFailures++;
        LastFailureReason = string.IsNullOrWhiteSpace(reason) ? "The charge failed." : reason.Trim();

        var willRetry = ConsecutiveFailures < MaxConsecutiveFailures;

        if (willRetry)
        {
            Status = RecurringDonationStatus.Retrying;

            var retryAt = now.AddDays(Math.Max(1, retryDays));

            // Never past the next ordinary cycle: a retry that landed after it would collide with
            // the cycle's own charge and produce two donations in one period.
            NextChargeAt = NextChargeAt is { } scheduled && scheduled < retryAt
                ? scheduled
                : retryAt;
        }
        else
        {
            Status = RecurringDonationStatus.Suspended;
            NextChargeAt = null;
        }

        Revision++;
        ModifiedAt = now;

        RaiseDomainEvent(new RecurringDonationFailedDomainEvent
        {
            ScheduleId = Id,
            DonorUserId = DonorUserId,
            CauseCode = CauseCode,
            AmountMinorUnits = AmountMinorUnits,
            Currency = Currency,
            FailureReason = LastFailureReason,
            WillRetry = willRetry,
            ConsecutiveFailures = ConsecutiveFailures,
            NextAttemptAt = NextChargeAt,
            FailedAt = now
        });
    }

    /// <summary>
    /// The donor stops it.
    /// <para>
    /// <b>Idempotent, and it has to be.</b> A cancel button tapped twice, a retried mobile request
    /// and a replayed webhook are the same instruction; answering the second with a 409 would tell
    /// somebody their cancellation failed when it had already worked.
    /// </para>
    /// </summary>
    public Result Cancel(DateTimeOffset now)
    {
        if (Status == RecurringDonationStatus.Cancelled)
        {
            return Result.Success();
        }

        if (Status == RecurringDonationStatus.Completed)
        {
            return Result.Failure(Error.Conflict(
                FinancialErrorCodes.CampaignTransitionInvalid,
                "This recurring donation has already ended."));
        }

        Status = RecurringDonationStatus.Cancelled;
        NextChargeAt = null;
        CancelledAt = now;
        Revision++;
        ModifiedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// Ends the schedule because the cause it gave to is closed.
    /// <para>
    /// The sweep's own decision rather than the donor's, so it is a different state and a different
    /// sentence on the screen.
    /// </para>
    /// </summary>
    public void CompleteBecauseCauseClosed(DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return;
        }

        Status = RecurringDonationStatus.Completed;
        NextChargeAt = null;
        LastFailureReason = "The cause this supported is no longer collecting.";
        Revision++;
        ModifiedAt = now;
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
