using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Availability;

/// <summary>
/// A provider's schedule for one day of the week.
/// <para>
/// The client's <c>businessHours: [{ dayOfWeek, openTime, closeTime, isClosed }]</c>, with the
/// one shape change that matters: several <see cref="AvailabilityWindow"/> per day instead of a
/// single open/close pair, so a split shift is representable. Renamed from "business hours"
/// because a church and an embassy do not have business hours, and the brief forbids hardcoding
/// the businesses-only assumption.
/// </para>
/// <para>
/// <b><see cref="IsClosed"/> is not the same as having no windows, and keeping them apart is
/// deliberate.</b> "Closed on Sundays" is a statement the provider made; "no hours recorded for
/// Sunday" is an absence of information. The client renders the first as "Closed" and should
/// render the second as nothing at all, and a model that collapsed them would make every
/// provider that has not finished its profile appear to be closed half the week.
/// </para>
/// </summary>
public sealed class OperatingHours : Entity<OperatingHoursId>
{
    private readonly List<AvailabilityWindow> _windows = [];

    private OperatingHours(OperatingHoursId id, DayOfWeek day, bool isClosed) : base(id)
    {
        Day = day;
        IsClosed = isClosed;
    }

    private OperatingHours() { }

    public DayOfWeek Day { get; private set; }

    /// <summary>An explicit statement that the provider does not open on this day.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>The periods the provider is open. Empty when <see cref="IsClosed"/>.</summary>
    public IReadOnlyList<AvailabilityWindow> Windows => _windows.AsReadOnly();

    /// <summary>A day with an explicit closure or at least one period. Anything else is unset.</summary>
    public bool IsSpecified => IsClosed || _windows.Count > 0;

    public static OperatingHours Closed(OperatingHoursId id, DayOfWeek day)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new OperatingHours(id, day, isClosed: true);
    }

    public static OperatingHours Open(OperatingHoursId id, DayOfWeek day)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new OperatingHours(id, day, isClosed: false);
    }

    /// <summary>
    /// Adds an opening period, refusing one that overlaps a period already recorded.
    /// <para>
    /// Refusing rather than merging, because two overlapping periods are almost always a typo —
    /// 09:00–17:00 entered twice, or 9–5 and 09:00–17:30 — and merging them silently produces a
    /// schedule the provider did not write and cannot see is wrong.
    /// </para>
    /// </summary>
    public Result<AvailabilityWindow> AddWindow(
        AvailabilityWindowId windowId, TimeOnly opensAt, TimeOnly closesAt, string? label = null)
    {
        if (_windows.Count >= ProviderLimits.MaxWindowsPerDay)
        {
            return Result.Failure<AvailabilityWindow>(ProviderErrors.OperatingHoursWindowLimit);
        }

        var window = AvailabilityWindow.Create(windowId, Id, opensAt, closesAt, label);

        if (window.IsFailure)
        {
            return window;
        }

        if (_windows.Any(existing => existing.OverlapsWith(window.Value)))
        {
            return Result.Failure<AvailabilityWindow>(ProviderErrors.OperatingHoursOverlap);
        }

        _windows.Add(window.Value);
        Sort();
        IsClosed = false;

        return window;
    }

    /// <summary>Marks the day explicitly closed and discards any periods recorded for it.</summary>
    public void Close()
    {
        _windows.Clear();
        IsClosed = true;
    }

    /// <summary>Clears the day back to "no information", which is not the same as closed.</summary>
    public void Clear()
    {
        _windows.Clear();
        IsClosed = false;
    }

    /// <summary>Whether the provider is open at a wall-clock time on this day.</summary>
    public bool IsOpenAt(TimeOnly time) =>
        !IsClosed && _windows.Any(window => window.Contains(time));

    /// <summary>
    /// The period that is running at a wall-clock time, so a client can say when it closes.
    /// "Open now — closes at 20:00" is materially more useful than "Open now".
    /// </summary>
    public AvailabilityWindow? WindowAt(TimeOnly time) =>
        IsClosed ? null : _windows.FirstOrDefault(window => window.Contains(time));

    /// <summary>
    /// The next period to start after a wall-clock time, so a client can say "Opens at 09:00".
    /// Null when nothing further opens today.
    /// </summary>
    public AvailabilityWindow? NextWindowAfter(TimeOnly time) =>
        IsClosed ? null : _windows.FirstOrDefault(window => window.OpensAt > time);

    /// <summary>
    /// The day's hours as one display string, so the client renders the same text B7 computes.
    /// <para>
    /// Produced server-side on purpose. The client's organization screen already stores hours as
    /// a pre-formatted string (<c>"09:00 - 17:00"</c>) because it had nowhere else to build one;
    /// returning both the structure and the string means that screen needs no change and every
    /// other client renders identically.
    /// </para>
    /// </summary>
    public string Describe()
    {
        if (IsClosed)
        {
            return "Closed";
        }

        return _windows.Count == 0
            ? string.Empty
            : string.Join(", ", _windows.Select(window =>
                $"{window.OpensAt:HH\\:mm} – {window.ClosesAt:HH\\:mm}"));
    }

    private void Sort() => _windows.Sort((left, right) => left.OpensAt.CompareTo(right.OpensAt));
}
