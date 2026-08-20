using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// How many people can come, and how many said they would.
/// <para>
/// <b>The counts live on the event rather than being derived from the RSVP table, and that is a
/// deliberate trade.</b> A count computed from a join is always right and costs a scan on every
/// card render; a count kept here is one number to keep honest and is the reason a list of forty
/// events is forty rows rather than forty-one queries. The single place it moves is
/// <c>CommunityEvent.SettleAttendance</c>, called only from the RSVP status handler — the same
/// discipline B11 applies to unit counts, for the same reason.
/// </para>
/// <para>
/// <b>A null <see cref="Maximum"/> means uncapped</b>, which is the honest default for a church
/// service or a street festival. Zero would mean "nobody may come", and defaulting to it would
/// make every event created without a capacity permanently full.
/// </para>
/// </summary>
public sealed class EventCapacity : ValueObject
{
    private EventCapacity(
        int? maximum, int goingCount, int interestedCount, int waitlistCount, int checkedInCount)
    {
        Maximum = maximum;
        GoingCount = goingCount;
        InterestedCount = interestedCount;
        WaitlistCount = waitlistCount;
        CheckedInCount = checkedInCount;
    }

    private EventCapacity() { }

    /// <summary>Uncapped, with nobody yet. What every event starts as.</summary>
    public static EventCapacity Unlimited => new(null, 0, 0, 0, 0);

    /// <summary>The ceiling, or null when there is not one.</summary>
    public int? Maximum { get; private set; }

    /// <summary>People holding a place. Includes those already through the door.</summary>
    public int GoingCount { get; private set; }

    /// <summary>People who might come. Holds no place.</summary>
    public int InterestedCount { get; private set; }

    public int WaitlistCount { get; private set; }

    public int CheckedInCount { get; private set; }

    public bool IsUnlimited => Maximum is null;

    public int? RemainingPlaces =>
        Maximum is { } max ? Math.Max(0, max - GoingCount) : null;

    public bool IsFull => Maximum is { } max && GoingCount >= max;

    /// <summary>What an organizer's dashboard leads with, and null before anybody came.</summary>
    public double? AttendanceRate =>
        GoingCount == 0 ? null : (double)CheckedInCount / GoingCount;

    public static Result<EventCapacity> Create(int? maximum)
    {
        if (maximum is { } max && max < 1)
        {
            return Result.Failure<EventCapacity>(CommunityLifeErrors.CapacityInvalid);
        }

        return Result.Success(new EventCapacity(maximum, 0, 0, 0, 0));
    }

    /// <summary>
    /// Restates the ceiling, keeping the counts.
    /// <para>
    /// Refused below what is already committed — an organizer cannot shrink a hall to forty while
    /// sixty people are holding places, because the sixty-first was told they had one.
    /// </para>
    /// </summary>
    public Result<EventCapacity> WithMaximum(int? maximum)
    {
        if (maximum is { } max)
        {
            if (max < 1)
            {
                return Result.Failure<EventCapacity>(CommunityLifeErrors.CapacityInvalid);
            }

            if (max < GoingCount)
            {
                return Result.Failure<EventCapacity>(
                    CommunityLifeErrors.CapacityBelowCommitted);
            }
        }

        return Result.Success(
            new EventCapacity(
                maximum, GoingCount, InterestedCount, WaitlistCount, CheckedInCount));
    }

    /// <summary>
    /// Applies a net movement across the four counters. Every count in the module changes here and
    /// nowhere else, which is what makes them reconcilable.
    /// </summary>
    public EventCapacity Apply(
        int going = 0, int interested = 0, int waitlisted = 0, int checkedIn = 0) =>
        new(
            Maximum,
            Math.Max(0, GoingCount + going),
            Math.Max(0, InterestedCount + interested),
            Math.Max(0, WaitlistCount + waitlisted),
            Math.Max(0, CheckedInCount + checkedIn));

    /// <summary>Restates the counters from a recount. The repair path, not the normal one.</summary>
    public EventCapacity Reconcile(
        int going, int interested, int waitlisted, int checkedIn) =>
        new(
            Maximum,
            Math.Max(0, going),
            Math.Max(0, interested),
            Math.Max(0, waitlisted),
            Math.Max(0, checkedIn));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Maximum;
        yield return GoingCount;
        yield return InterestedCount;
        yield return WaitlistCount;
        yield return CheckedInCount;
    }
}
