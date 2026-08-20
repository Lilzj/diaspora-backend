using Diaspora.Modules.Providers.Domain.Availability;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Extensibility.Availability;

namespace Diaspora.Modules.Providers.Infrastructure.Availability;

/// <summary>
/// Answers availability from the provider's own weekly schedule.
/// <para>
/// The implementation every deployment gets, and the one that runs <b>last</b> — at order 1000 —
/// so anything with better information overrides it. When a booking module ships, a restaurant's
/// card can say "fully booked tonight" without B7 knowing what a booking is; until then, this
/// answers the question the schedule can answer.
/// </para>
/// <para>
/// <b>It never returns "closed" for a provider that has told us nothing.</b> An appointment-only
/// consultant, a community group that meets by arrangement, a provider halfway through its
/// profile — all of them get a null or an explanatory phrase rather than a false "Closed" that
/// costs them business. That distinction does not exist in the client today, which is exactly why
/// every one of its mock providers has <c>isOpenNow</c> hard-coded.
/// </para>
/// </summary>
public sealed class ScheduleAvailabilityProvider : IProviderAvailabilityProvider
{
    public string Module => ProvidersModuleMetadata.Name;

    public int Order => 1_000;

    public Task<AvailabilityAnswer> EvaluateAsync(
        AvailabilityQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var provider = query.Provider;

        if (provider.Status is not ProviderStatus.Active)
        {
            return Task.FromResult(AvailabilityAnswer.Closed(
                Module, reason: DescribeStatus(provider.Status)));
        }

        return Task.FromResult(provider.Availability.Kind switch
        {
            AvailabilityKind.AlwaysOpen => AvailabilityAnswer.Open(Module, reason: "Open 24 hours"),

            AvailabilityKind.Unavailable =>
                AvailabilityAnswer.Closed(Module, reason: "Temporarily unavailable"),

            AvailabilityKind.AppointmentOnly =>
                AvailabilityAnswer.NotApplicable(Module, "By appointment"),

            AvailabilityKind.ByArrangement =>
                AvailabilityAnswer.NotApplicable(Module, "By arrangement"),

            AvailabilityKind.EmergencyService =>
                EvaluateSchedule(provider, query.At, fallbackReason: "Emergency callouts anytime"),

            _ => EvaluateSchedule(provider, query.At, fallbackReason: null)
        });
    }

    /// <summary>
    /// Reads the weekly schedule and any dated exception, in the provider's own zone.
    /// <para>
    /// The aggregate owns the decision — see <see cref="Provider.IsOpenAt"/> — and this method's
    /// job is to turn its three-valued answer into something with a closing time and a phrase
    /// attached. Keeping the rule in the aggregate is what lets it be unit-tested without a
    /// registry, a clock or a container.
    /// </para>
    /// </summary>
    private AvailabilityAnswer EvaluateSchedule(
        Provider provider, DateTimeOffset at, string? fallbackReason)
    {
        var isOpen = provider.IsOpenAt(at);

        if (isOpen is null)
        {
            // The provider has recorded nothing for today. Saying nothing is right: a fabricated
            // "Closed" is a claim on their behalf that they never made.
            return AvailabilityAnswer.NotApplicable(Module, fallbackReason);
        }

        var local = provider.Availability.ToLocal(at);
        var localTime = TimeOnly.FromDateTime(local.DateTime);
        var localDate = DateOnly.FromDateTime(local.DateTime);

        var governing = provider.AvailabilityExceptions
            .FirstOrDefault(exception => exception.Covers(localDate));

        if (governing is not null)
        {
            return isOpen.Value
                ? AvailabilityAnswer.Open(Module, reason: governing.Reason)
                : AvailabilityAnswer.Closed(Module, reason: governing.Reason ?? "Closed today");
        }

        var hours = provider.OperatingHours.FirstOrDefault(day => day.Day == local.DayOfWeek);

        if (hours is null)
        {
            return AvailabilityAnswer.NotApplicable(Module, fallbackReason);
        }

        return isOpen.Value
            ? AvailabilityAnswer.Open(Module, ClosingAt(hours, localTime, local), fallbackReason)
            : AvailabilityAnswer.Closed(Module, OpeningAt(hours, localTime, local), fallbackReason);
    }

    /// <summary>
    /// When the current period ends, so a client can render "Open now — closes at 20:00".
    /// <para>
    /// An overnight period closes on the following day, which is why the date is advanced rather
    /// than the time simply substituted — a bar open until 02:00 does not close two hours ago.
    /// </para>
    /// </summary>
    private static DateTimeOffset? ClosingAt(
        OperatingHours hours, TimeOnly localTime, DateTimeOffset local)
    {
        var window = hours.WindowAt(localTime);

        if (window is null)
        {
            return null;
        }

        var closesOnNextDay = window.CrossesMidnight && localTime >= window.OpensAt;

        return Combine(local, window.ClosesAt, closesOnNextDay ? 1 : 0);
    }

    /// <summary>When the next period starts today, so a client can render "Opens at 09:00".</summary>
    private static DateTimeOffset? OpeningAt(
        OperatingHours hours, TimeOnly localTime, DateTimeOffset local)
    {
        var next = hours.NextWindowAfter(localTime);

        return next is null ? null : Combine(local, next.OpensAt, 0);
    }

    private static DateTimeOffset Combine(DateTimeOffset local, TimeOnly time, int addDays) =>
        new(
            local.Date.AddDays(addDays).Add(time.ToTimeSpan()),
            local.Offset);

    private static string DescribeStatus(ProviderStatus status) => status switch
    {
        ProviderStatus.Paused => "Temporarily closed",
        ProviderStatus.Closed => "Permanently closed",
        ProviderStatus.Suspended => "Currently unavailable",
        _ => "Not yet open"
    };
}
