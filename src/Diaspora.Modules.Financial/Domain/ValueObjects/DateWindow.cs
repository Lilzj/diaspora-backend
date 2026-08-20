using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// A period something is in force for.
/// <para>
/// One type for a placement window, a billing period, a promotion's validity and a policy version's
/// effective range, because four types with identical fields is four places for an off-by-one to
/// live independently. The end is <b>exclusive</b>, stated here because that is the decision every
/// caller inherits and the one nobody writes down.
/// </para>
/// <para>
/// A null <see cref="EndsAt"/> means "no end", which is a real answer for a lifetime plan and a
/// standing policy — not a missing value.
/// </para>
/// </summary>
public sealed class DateWindow : ValueObject
{
    private DateWindow(DateTimeOffset startsAt, DateTimeOffset? endsAt)
    {
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    private DateWindow() { }

    public DateTimeOffset StartsAt { get; private set; }

    /// <summary>Exclusive. Null means the window never closes.</summary>
    public DateTimeOffset? EndsAt { get; private set; }

    public static DateWindow Create(DateTimeOffset startsAt, DateTimeOffset? endsAt)
    {
        if (endsAt is { } end && end <= startsAt)
        {
            throw new ArgumentException(
                "A window must end after it starts.", nameof(endsAt));
        }

        return new DateWindow(startsAt, endsAt);
    }

    /// <summary>A window that starts now and runs for a number of whole days.</summary>
    public static DateWindow ForDays(DateTimeOffset startsAt, int days)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(days);

        return new DateWindow(startsAt, startsAt.AddDays(days));
    }

    public static DateWindow Open(DateTimeOffset startsAt) => new(startsAt, null);

    public bool Contains(DateTimeOffset moment) =>
        moment >= StartsAt && (EndsAt is not { } end || moment < end);

    public bool HasStarted(DateTimeOffset moment) => moment >= StartsAt;

    public bool HasEnded(DateTimeOffset moment) => EndsAt is { } end && moment >= end;

    /// <summary>The same window ending sooner. Used when a placement is cancelled mid-run.</summary>
    public DateWindow EndingAt(DateTimeOffset endsAt) =>
        new(StartsAt, endsAt < StartsAt ? StartsAt : endsAt);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StartsAt;
        yield return EndsAt;
    }
}
