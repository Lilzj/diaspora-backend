using Diaspora.Modules.Providers.Domain.Providers;

namespace Diaspora.Modules.Providers.Extensibility.Availability;

/// <summary>
/// How a module answers "is this provider available" with knowledge B7 does not have.
/// <para>
/// <b>The seam that keeps B7 out of the scheduling business.</b> B7 owns the weekly schedule,
/// dated exceptions, time zones and an open-now calculation over them, and that is genuinely all
/// a directory needs. It emphatically does not own appointments, capacity, staff rotas, table
/// reservations or queue length — and the brief says so ("Future scheduling integration"). When
/// a booking module lands, it registers one of these and a restaurant's card can say "fully
/// booked tonight" without B7 knowing what a booking is.
/// </para>
/// <para>
/// The shipped implementation, <c>ScheduleAvailabilityProvider</c>, answers purely from the
/// aggregate. A registered provider that returns <see cref="AvailabilityAnswer.Unknown"/> hands
/// back to it, so a booking integration that only covers restaurants leaves every other provider
/// answering exactly as it does today.
/// </para>
/// </summary>
public interface IProviderAvailabilityProvider
{
    /// <summary>The module contributing.</summary>
    string Module { get; }

    /// <summary>
    /// Lower runs first, and the first decisive answer wins. The schedule-based provider is
    /// deliberately last, at 1000, so anything with better information overrides it.
    /// </summary>
    int Order => 100;

    /// <summary>Whether this provider has anything to say about this one. A cheap check.</summary>
    bool AppliesTo(Provider provider) => true;

    /// <summary>
    /// Answers for one provider at one instant.
    /// <para>
    /// <see cref="AvailabilityAnswer.Unknown"/> rather than false when the provider has no
    /// opinion. The difference reaches the client: false renders "Closed", unknown renders
    /// nothing, and a booking integration with no data for a barber must not tell members the
    /// barber is shut.
    /// </para>
    /// </summary>
    Task<AvailabilityAnswer> EvaluateAsync(
        AvailabilityQuery query, CancellationToken cancellationToken = default);
}
