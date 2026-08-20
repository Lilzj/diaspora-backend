using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Availability;

/// <summary>
/// A dated override of the weekly schedule.
/// <para>
/// The brief's holiday overrides and temporary closures. Without them, "open now" is wrong on
/// every public holiday, during every refurbishment and through every family bereavement — and
/// wrong in the direction that sends a member across London to a locked door.
/// </para>
/// <para>
/// A date <em>range</em> rather than a single date, because a two-week closure is one statement
/// the provider makes and not fourteen. <see cref="StartsOn"/> and <see cref="EndsOn"/> are
/// inclusive, which is what a person means by "closed from the 20th to the 27th".
/// </para>
/// <para>
/// Both a closure and a change of hours live here, distinguished by <see cref="IsClosed"/>. A
/// separate type for each would have doubled every code path that asks "does an exception apply
/// today", and the answer is the same shape either way: the weekly schedule is set aside and
/// this is used instead.
/// </para>
/// </summary>
public sealed class AvailabilityException : Entity<AvailabilityExceptionId>
{
    public const int MaxReasonLength = 200;

    private AvailabilityException(
        AvailabilityExceptionId id,
        AvailabilityExceptionKind kind,
        DateOnly startsOn,
        DateOnly endsOn,
        bool isClosed)
        : base(id)
    {
        Kind = kind;
        StartsOn = startsOn;
        EndsOn = endsOn;
        IsClosed = isClosed;
    }

    private AvailabilityException() { }

    public AvailabilityExceptionKind Kind { get; private set; }

    /// <summary>Inclusive.</summary>
    public DateOnly StartsOn { get; private set; }

    /// <summary>Inclusive. Equal to <see cref="StartsOn"/> for a single day.</summary>
    public DateOnly EndsOn { get; private set; }

    /// <summary>Whether the provider is shut for these dates, or merely on different hours.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Replacement opening time. Set only when <see cref="IsClosed"/> is false.</summary>
    public TimeOnly? OpensAt { get; private set; }

    /// <summary>Replacement closing time. Set only when <see cref="IsClosed"/> is false.</summary>
    public TimeOnly? ClosesAt { get; private set; }

    /// <summary>What to show the member. "Closed for Eid", "Staff training", "Late opening".</summary>
    public string? Reason { get; private set; }

    public static Result<AvailabilityException> Closure(
        AvailabilityExceptionId id,
        AvailabilityExceptionKind kind,
        DateOnly startsOn,
        DateOnly endsOn,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        return endsOn < startsOn
            ? Result.Failure<AvailabilityException>(ProviderErrors.AvailabilityExceptionRangeInverted)
            : Result.Success(new AvailabilityException(id, kind, startsOn, endsOn, isClosed: true)
            {
                Reason = Trim(reason)
            });
    }

    public static Result<AvailabilityException> SpecialHours(
        AvailabilityExceptionId id,
        AvailabilityExceptionKind kind,
        DateOnly startsOn,
        DateOnly endsOn,
        TimeOnly opensAt,
        TimeOnly closesAt,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (endsOn < startsOn)
        {
            return Result.Failure<AvailabilityException>(ProviderErrors.AvailabilityExceptionRangeInverted);
        }

        return opensAt == closesAt
            ? Result.Failure<AvailabilityException>(ProviderErrors.OperatingHoursWindowInverted)
            : Result.Success(new AvailabilityException(id, kind, startsOn, endsOn, isClosed: false)
            {
                OpensAt = opensAt,
                ClosesAt = closesAt,
                Reason = Trim(reason)
            });
    }

    /// <summary>Whether this exception governs a given date.</summary>
    public bool Covers(DateOnly date) => date >= StartsOn && date <= EndsOn;

    /// <summary>Whether two exceptions claim any of the same days.</summary>
    public bool OverlapsWith(AvailabilityException other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return StartsOn <= other.EndsOn && other.StartsOn <= EndsOn;
    }

    /// <summary>Whether the provider is open at a wall-clock time on a date this exception covers.</summary>
    public bool IsOpenAt(TimeOnly time)
    {
        if (IsClosed || OpensAt is not { } opens || ClosesAt is not { } closes)
        {
            return false;
        }

        return closes <= opens
            ? time >= opens || time < closes
            : time >= opens && time < closes;
    }

    /// <summary>Whether this exception is entirely in the past and may be swept.</summary>
    public bool HasLapsed(DateOnly today) => EndsOn < today;

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, MaxReasonLength)];
}
