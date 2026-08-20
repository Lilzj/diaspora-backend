using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// What the parties to a reservation say they have agreed.
/// <para>
/// <b>Recorded, never enforced, and never interpreted.</b> Housing stores the dates, the figure
/// and the currency because a dispute six weeks later turns on exactly those and on nothing else
/// the platform holds. It does not model a tenancy, calculate a schedule, apportion a deposit, or
/// read a document — lease management, payment processing and digital signatures are all
/// explicitly out of scope, and this type is where that boundary is visible.
/// </para>
/// <para>
/// <b>The agreed rent is stored separately from the listing's asking rent</b> for the same reason
/// B10 stores an agreed price: the listing's figure may change afterwards, and the arrangement was
/// made at this one. It is the single number a disagreement is actually about.
/// </para>
/// <para>
/// <b>The hold expiry is the field that keeps the market working.</b> Every live reservation is a
/// property nobody else can take, so a hold with no end is a home removed from the platform by
/// somebody who stopped replying. The handler supplies a default when the parties agreed none.
/// </para>
/// </summary>
public sealed class ReservationTerms : ValueObject
{
    private ReservationTerms(
        DateTimeOffset? moveInDate,
        DateTimeOffset? moveOutDate,
        DateTimeOffset? holdExpiresAt,
        decimal? agreedRent,
        decimal? agreedDeposit,
        string? currencyCode,
        RentPeriod period,
        LeaseType leaseType,
        int occupants)
    {
        MoveInDate = moveInDate;
        MoveOutDate = moveOutDate;
        HoldExpiresAt = holdExpiresAt;
        AgreedRent = agreedRent;
        AgreedDeposit = agreedDeposit;
        CurrencyCode = currencyCode;
        Period = period;
        LeaseType = leaseType;
        Occupants = occupants;
    }

    private ReservationTerms() { }

    /// <summary>When the member intends to move in.</summary>
    public DateTimeOffset? MoveInDate { get; private set; }

    /// <summary>When they intend to leave. Null for an open-ended arrangement.</summary>
    public DateTimeOffset? MoveOutDate { get; private set; }

    /// <summary>
    /// When the hold lapses if nothing happens. The sweep's work list, and the reason a quiet
    /// member cannot take a property off the market indefinitely.
    /// </summary>
    public DateTimeOffset? HoldExpiresAt { get; private set; }

    /// <summary>What the parties said the rent would be. See the class summary.</summary>
    public decimal? AgreedRent { get; private set; }

    public decimal? AgreedDeposit { get; private set; }

    public string? CurrencyCode { get; private set; }

    public RentPeriod Period { get; private set; }

    public LeaseType LeaseType { get; private set; }

    /// <summary>How many people will live there. Checked against the listing's stated maximum.</summary>
    public int Occupants { get; private set; }

    /// <summary>
    /// The intended stay in nights, where both dates are known. What the listing's minimum and
    /// maximum stay are checked against.
    /// </summary>
    public int? StayNights =>
        MoveInDate is { } from && MoveOutDate is { } until && until > from
            ? (int)Math.Ceiling((until - from).TotalDays)
            : null;

    /// <summary>Whether the hold has run out. What the expiry sweep asks.</summary>
    public bool HasLapsed(DateTimeOffset now) =>
        HoldExpiresAt is { } expiry && expiry <= now;

    public static Result<ReservationTerms> Create(
        DateTimeOffset? moveInDate,
        DateTimeOffset? moveOutDate,
        DateTimeOffset? holdExpiresAt,
        decimal? agreedRent = null,
        decimal? agreedDeposit = null,
        string? currencyCode = null,
        RentPeriod period = RentPeriod.None,
        LeaseType leaseType = LeaseType.Unspecified,
        int occupants = 1)
    {
        if (moveInDate is { } from && moveOutDate is { } until && until <= from)
        {
            return Result.Failure<ReservationTerms>(HousingErrors.ReservationWindowInvalid);
        }

        if (occupants < 1 || occupants > HousingLimits.MaxOccupants)
        {
            return Result.Failure<ReservationTerms>(HousingErrors.OccupancyInvalid);
        }

        if (agreedRent is { } rent && (rent < 0m || rent > HousingLimits.MaxAmount))
        {
            return Result.Failure<ReservationTerms>(HousingErrors.PriceInvalid);
        }

        if (agreedDeposit is { } deposit
            && (deposit < 0m || deposit > HousingLimits.MaxAmount))
        {
            return Result.Failure<ReservationTerms>(HousingErrors.DepositInvalid);
        }

        var currency = HousingPrice.NormalizeCurrency(currencyCode);

        // A figure with no currency is a number nobody can act on across the borders this
        // platform spans. A currency with no figure is harmless and is kept.
        if (agreedRent is not null && currency is null)
        {
            return Result.Failure<ReservationTerms>(HousingErrors.CurrencyInvalid);
        }

        return Result.Success(new ReservationTerms(
            moveInDate,
            moveOutDate,
            holdExpiresAt,
            agreedRent is { } value ? decimal.Round(value, 2) : null,
            agreedDeposit is { } depositValue ? decimal.Round(depositValue, 2) : null,
            currency,
            period,
            leaseType,
            occupants));
    }

    /// <summary>Extends or shortens the hold, leaving everything else alone.</summary>
    public ReservationTerms WithHoldExpiry(DateTimeOffset? holdExpiresAt) =>
        new(MoveInDate,
            MoveOutDate,
            holdExpiresAt,
            AgreedRent,
            AgreedDeposit,
            CurrencyCode,
            Period,
            LeaseType,
            Occupants);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MoveInDate;
        yield return MoveOutDate;
        yield return HoldExpiresAt;
        yield return AgreedRent;
        yield return AgreedDeposit;
        yield return CurrencyCode;
        yield return Period;
        yield return LeaseType;
        yield return Occupants;
    }
}
