using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Availability;

/// <summary>
/// One period a provider is open.
/// <para>
/// A period rather than an open/close pair on the day, because a split shift is the ordinary
/// case here and not an edge one: a restaurant that serves lunch 12:00–15:00 and dinner
/// 18:00–23:00 is closed at four o'clock, and the client's
/// <c>{ openTime, closeTime, isClosed }</c> shape can only say it is open from noon to eleven.
/// A member who turns up at four because the app said "Open now" is the failure this type
/// exists to prevent.
/// </para>
/// <para>
/// <b>Overnight periods are supported and are why <see cref="Contains"/> is not a simple
/// comparison.</b> A late bar open 20:00–02:00 has a closing time numerically before its
/// opening time, and treating that as an inverted range would refuse to store the hours of
/// every venue that stays open past midnight. When <see cref="ClosesAt"/> is at or before
/// <see cref="OpensAt"/> the period is read as crossing midnight.
/// </para>
/// <para>
/// Times are <see cref="TimeOnly"/> — a wall-clock time with no date and no offset — because
/// that is exactly what "we open at nine" means. The zone that turns it into an instant lives
/// once on <c>AvailabilitySettings</c>, and a period that carried its own offset would be wrong
/// twice a year in every country that changes its clocks.
/// </para>
/// </summary>
public sealed class AvailabilityWindow : Entity<AvailabilityWindowId>
{
    private AvailabilityWindow(
        AvailabilityWindowId id, OperatingHoursId operatingHoursId, TimeOnly opensAt, TimeOnly closesAt)
        : base(id)
    {
        OperatingHoursId = operatingHoursId;
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    private AvailabilityWindow() { }

    public OperatingHoursId OperatingHoursId { get; private set; } = default!;

    public TimeOnly OpensAt { get; private set; }

    public TimeOnly ClosesAt { get; private set; }

    /// <summary>What the provider calls this period. "Lunch", "Dinner", "Surgery hours".</summary>
    public string? Label { get; private set; }

    /// <summary>Whether this period runs past midnight into the following day.</summary>
    public bool CrossesMidnight => ClosesAt <= OpensAt;

    internal static Result<AvailabilityWindow> Create(
        AvailabilityWindowId id,
        OperatingHoursId operatingHoursId,
        TimeOnly opensAt,
        TimeOnly closesAt,
        string? label = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(operatingHoursId);

        // Equal times are the only genuinely meaningless period: a window of zero length that is
        // also not an overnight one. Everything else, including 20:00–02:00, is legitimate.
        return opensAt == closesAt
            ? Result.Failure<AvailabilityWindow>(ProviderErrors.OperatingHoursWindowInverted)
            : Result.Success(new AvailabilityWindow(id, operatingHoursId, opensAt, closesAt)
            {
                Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim()
            });
    }

    /// <summary>Whether a wall-clock time falls inside this period, midnight crossing included.</summary>
    public bool Contains(TimeOnly time) =>
        CrossesMidnight
            ? time >= OpensAt || time < ClosesAt
            : time >= OpensAt && time < ClosesAt;

    /// <summary>
    /// Whether two periods on the same day overlap.
    /// <para>
    /// Compared on the minute-of-day intervals rather than on the raw times, because an
    /// overnight period occupies two intervals and comparing endpoints directly reports
    /// 20:00–02:00 and 01:00–03:00 as disjoint when they plainly are not.
    /// </para>
    /// </summary>
    public bool OverlapsWith(AvailabilityWindow other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var (mineStart, mineEnd) in Intervals())
        {
            foreach (var (theirsStart, theirsEnd) in other.Intervals())
            {
                if (mineStart < theirsEnd && theirsStart < mineEnd)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>How long this period lasts, midnight crossing included.</summary>
    public TimeSpan Duration =>
        CrossesMidnight
            ? TimeSpan.FromDays(1) - (OpensAt - ClosesAt)
            : ClosesAt - OpensAt;

    /// <summary>
    /// The period as one or two minute-of-day intervals. An overnight period becomes
    /// [open, 1440) and [0, close).
    /// </summary>
    private IEnumerable<(int Start, int End)> Intervals()
    {
        const int MinutesInDay = 24 * 60;

        var open = (OpensAt.Hour * 60) + OpensAt.Minute;
        var close = (ClosesAt.Hour * 60) + ClosesAt.Minute;

        if (CrossesMidnight)
        {
            yield return (open, MinutesInDay);
            yield return (0, close);
        }
        else
        {
            yield return (open, close);
        }
    }
}
