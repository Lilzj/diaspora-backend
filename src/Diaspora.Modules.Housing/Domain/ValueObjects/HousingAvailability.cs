using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// When the property can be moved into, for how long, and how much of it is left.
/// <para>
/// <b>This is the module's scarce resource, and it is a counter rather than a flag for one
/// reason: a hostel is not a flat.</b> One advert may be a single home, in which case
/// <see cref="TotalUnits"/> is one and the counter degenerates to a boolean; or it may be twelve
/// identical beds in a dormitory, or eight studios in one block let on one listing. Modelling
/// only the first case would force a hostel to create twelve listings that differ in nothing, and
/// would make "how many beds are left tonight" a question the platform answers by counting rows.
/// </para>
/// <para>
/// <b>The three counts always sum to the truth.</b> <see cref="ReservedUnits"/> is held for named
/// members, <see cref="OccupiedUnits"/> is lived in, and <see cref="AvailableUnits"/> is what
/// remains. Every reservation exit moves a unit between exactly two of them and no exit loses one
/// — a unit that is neither on the market nor accounted for is how a housing platform quietly
/// stops being able to let a property at all.
/// </para>
/// <para>
/// <b>On concurrency, and why nothing in this file had to change when it was solved.</b> Two
/// members reserving the last bed in the same instant is a genuine race, and B10 closed its
/// equivalent by recommending a guarded atomic counter primitive in <c>Diaspora.Persistence</c>.
/// Per the B11 brief, Housing did not invent a private one: the value object exposes
/// <see cref="TryHold"/> as a pure, total function over the counts, and the decision about how that
/// transition is made durable belongs entirely to <c>IHousingUnitReservationGuard</c> in the
/// persistence layer. B1.3 built the primitive, that port's implementation became a guarded
/// <c>UPDATE</c> the database arbitrates, and this file was not touched — which is what a seam
/// declared in the right place is worth.
/// </para>
/// <para>
/// <b><see cref="AvailableFrom"/> and <see cref="AvailableUntil"/> are the availability calendar's
/// future-ready abstraction, and deliberately the whole of it.</b> A real calendar — per-date
/// blocks, seasonal rates, minimum-stay-by-weekday — is a booking engine, which is out of scope.
/// What is in scope is that nothing above this type asks a listing for a date range directly:
/// callers ask <see cref="IsAvailableOn"/> and <see cref="AcceptsStay"/>, so a calendar module can
/// later answer those questions from a table of dates without a single caller changing.
/// </para>
/// </summary>
public sealed class HousingAvailability : ValueObject
{
    private HousingAvailability(
        DateTimeOffset? availableFrom,
        DateTimeOffset? availableUntil,
        int totalUnits,
        int reservedUnits,
        int occupiedUnits,
        int? minimumStayNights,
        int? maximumStayNights)
    {
        AvailableFrom = availableFrom;
        AvailableUntil = availableUntil;
        TotalUnits = totalUnits;
        ReservedUnits = reservedUnits;
        OccupiedUnits = occupiedUnits;
        MinimumStayNights = minimumStayNights;
        MaximumStayNights = maximumStayNights;
    }

    private HousingAvailability() { }

    /// <summary>When it can be moved into. Null means "now".</summary>
    public DateTimeOffset? AvailableFrom { get; private set; }

    /// <summary>When it stops being available. Null means "open-ended", the usual case.</summary>
    public DateTimeOffset? AvailableUntil { get; private set; }

    /// <summary>How many identical units this listing advertises. One for a single home.</summary>
    public int TotalUnits { get; private set; }

    /// <summary>Units held for named members by a live reservation.</summary>
    public int ReservedUnits { get; private set; }

    /// <summary>Units somebody has moved into.</summary>
    public int OccupiedUnits { get; private set; }

    /// <summary>The shortest stay accepted, in nights. Null when the landlord did not say.</summary>
    public int? MinimumStayNights { get; private set; }

    /// <summary>The longest stay accepted, in nights. Null for an open-ended tenancy.</summary>
    public int? MaximumStayNights { get; private set; }

    /// <summary>What is left. Never negative — the transitions maintain the invariant.</summary>
    public int AvailableUnits => Math.Max(0, TotalUnits - ReservedUnits - OccupiedUnits);

    /// <summary>Whether anything at all remains to be reserved.</summary>
    public bool HasUnitsRemaining => AvailableUnits > 0;

    /// <summary>Whether every unit is spoken for. What moves a listing into Reserved or Occupied.</summary>
    public bool IsFullyCommitted => AvailableUnits == 0 && TotalUnits > 0;

    /// <summary>Whether every unit is lived in, as opposed to merely held.</summary>
    public bool IsFullyOccupied => OccupiedUnits >= TotalUnits && TotalUnits > 0;

    /// <summary>A single home, which is the overwhelmingly common shape.</summary>
    public bool IsSingleUnit => TotalUnits == 1;

    /// <summary>
    /// A single unit, available now, on open-ended terms. The default for a new listing.
    /// <para>
    /// <b>A fresh instance per access, never a cached one.</b> This is an owned type, and a
    /// shared static instance assigned to two listings makes EF treat one as a duplicate of the
    /// other and write nulls into its columns — a defect the platform has carried since B6.
    /// </para>
    /// </summary>
    public static HousingAvailability Immediate => new(null, null, 1, 0, 0, null, null);

    public static Result<HousingAvailability> Create(
        DateTimeOffset? availableFrom = null,
        DateTimeOffset? availableUntil = null,
        int totalUnits = 1,
        int? minimumStayNights = null,
        int? maximumStayNights = null)
    {
        if (totalUnits < 1 || totalUnits > HousingLimits.MaxUnits)
        {
            return Result.Failure<HousingAvailability>(HousingErrors.UnitsInvalid);
        }

        if (availableFrom is { } from && availableUntil is { } until && until <= from)
        {
            return Result.Failure<HousingAvailability>(HousingErrors.AvailabilityWindowInvalid);
        }

        var stay = ValidateStay(minimumStayNights, maximumStayNights);

        if (stay.IsFailure)
        {
            return Result.Failure<HousingAvailability>(stay.Error);
        }

        return Result.Success(new HousingAvailability(
            availableFrom,
            availableUntil,
            totalUnits,
            reservedUnits: 0,
            occupiedUnits: 0,
            minimumStayNights,
            maximumStayNights));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Calendar questions
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether the property can be moved into on a given date.
    /// <para>
    /// The seam a real availability calendar replaces. Callers ask this rather than reading
    /// <see cref="AvailableFrom"/>, so a per-date implementation later is an implementation change.
    /// </para>
    /// </summary>
    public bool IsAvailableOn(DateTimeOffset moment) =>
        (AvailableFrom is not { } from || moment >= from)
        && (AvailableUntil is not { } until || moment <= until);

    /// <summary>Whether a stay of this many nights fits the landlord's terms.</summary>
    public bool AcceptsStay(int nights)
    {
        if (nights < 1)
        {
            return false;
        }

        return (MinimumStayNights is not { } minimum || nights >= minimum)
            && (MaximumStayNights is not { } maximum || nights <= maximum);
    }

    /// <summary>
    /// Whether a member could reserve now: units remain, and the property is not already past its
    /// availability window. A property whose move-in date is still ahead is deliberately
    /// reservable — booking a June flat in March is the single most common thing a relocating
    /// member does.
    /// </summary>
    public bool AcceptsReservationAt(DateTimeOffset moment) =>
        HasUnitsRemaining && (AvailableUntil is not { } until || moment <= until);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Transitions
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Holds units for a reservation.
    /// <para>
    /// <b>Pure and total: it decides, it does not persist.</b> Whether this decision survives a
    /// concurrent one is the guard port's question, not this type's — see the class summary.
    /// </para>
    /// </summary>
    public Result<HousingAvailability> TryHold(int units)
    {
        if (units < 1)
        {
            return Result.Failure<HousingAvailability>(HousingErrors.UnitsInvalid);
        }

        if (units > AvailableUnits)
        {
            return Result.Failure<HousingAvailability>(HousingErrors.NoUnitsAvailable);
        }

        return Result.Success(With(reservedUnits: ReservedUnits + units));
    }

    /// <summary>
    /// Returns held units to the market. A cancelled, declined or expired reservation.
    /// <para>
    /// Clamped at zero rather than failing. A release that would go negative means the counts had
    /// already drifted, and refusing it would leave the units held forever — the failure mode this
    /// method exists to prevent.
    /// </para>
    /// </summary>
    public HousingAvailability Release(int units) =>
        With(reservedUnits: Math.Max(0, ReservedUnits - Math.Max(0, units)));

    /// <summary>
    /// Converts held units into occupied ones. Somebody moved in.
    /// <para>
    /// The only transition that does not return units to the market, because they have not become
    /// free — they have become somebody's home.
    /// </para>
    /// </summary>
    public HousingAvailability Occupy(int units)
    {
        var moving = Math.Clamp(units, 0, ReservedUnits);

        return With(
            reservedUnits: ReservedUnits - moving,
            occupiedUnits: Math.Min(TotalUnits, OccupiedUnits + moving));
    }

    /// <summary>
    /// A tenancy ended and the unit is back on the market.
    /// <para>
    /// The transition that has no counterpart in B10, and the reason occupation is not terminal
    /// here: a sold bicycle does not come back, and a let flat always does.
    /// </para>
    /// </summary>
    public HousingAvailability Vacate(int units) =>
        With(occupiedUnits: Math.Max(0, OccupiedUnits - Math.Max(0, units)));

    /// <summary>
    /// Restates how many units the listing advertises.
    /// <para>
    /// Refused below what is already committed. Allowing it would produce a listing claiming four
    /// beds with five people in them, and the counts would never reconcile again.
    /// </para>
    /// </summary>
    public Result<HousingAvailability> WithTotalUnits(int totalUnits)
    {
        if (totalUnits < 1 || totalUnits > HousingLimits.MaxUnits)
        {
            return Result.Failure<HousingAvailability>(HousingErrors.UnitsInvalid);
        }

        if (totalUnits < ReservedUnits + OccupiedUnits)
        {
            return Result.Failure<HousingAvailability>(HousingErrors.UnitsBelowCommitted);
        }

        return Result.Success(With(totalUnits: totalUnits));
    }

    /// <summary>Restates the availability window, keeping every count as it is.</summary>
    public Result<HousingAvailability> WithWindow(
        DateTimeOffset? availableFrom,
        DateTimeOffset? availableUntil,
        int? minimumStayNights,
        int? maximumStayNights)
    {
        if (availableFrom is { } from && availableUntil is { } until && until <= from)
        {
            return Result.Failure<HousingAvailability>(HousingErrors.AvailabilityWindowInvalid);
        }

        var stay = ValidateStay(minimumStayNights, maximumStayNights);

        if (stay.IsFailure)
        {
            return Result.Failure<HousingAvailability>(stay.Error);
        }

        return Result.Success(new HousingAvailability(
            availableFrom,
            availableUntil,
            TotalUnits,
            ReservedUnits,
            OccupiedUnits,
            minimumStayNights,
            maximumStayNights));
    }

    private HousingAvailability With(
        int? totalUnits = null, int? reservedUnits = null, int? occupiedUnits = null) =>
        new(AvailableFrom,
            AvailableUntil,
            totalUnits ?? TotalUnits,
            reservedUnits ?? ReservedUnits,
            occupiedUnits ?? OccupiedUnits,
            MinimumStayNights,
            MaximumStayNights);

    private static Result ValidateStay(int? minimumStayNights, int? maximumStayNights)
    {
        if (minimumStayNights is { } minimum
            && (minimum < 1 || minimum > HousingLimits.MaxStayNights))
        {
            return Result.Failure(HousingErrors.StayLengthInvalid);
        }

        if (maximumStayNights is { } maximum
            && (maximum < 1 || maximum > HousingLimits.MaxStayNights))
        {
            return Result.Failure(HousingErrors.StayLengthInvalid);
        }

        if (minimumStayNights is { } floor && maximumStayNights is { } ceiling && ceiling < floor)
        {
            return Result.Failure(HousingErrors.StayLengthInvalid);
        }

        return Result.Success();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AvailableFrom;
        yield return AvailableUntil;
        yield return TotalUnits;
        yield return ReservedUnits;
        yield return OccupiedUnits;
        yield return MinimumStayNights;
        yield return MaximumStayNights;
    }
}
