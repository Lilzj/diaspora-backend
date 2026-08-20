namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// Everything about when a provider is available.
/// <para>
/// <b><see cref="IsOpenNow"/> is nullable, and that is the most important decision in this
/// contract.</b> The client's field is a boolean, which forces every provider with no meaningful
/// open-now answer — an appointment-only consultant, a provider that has not filled in its hours
/// — to be rendered as "Closed". That is wrong in the direction that loses the provider
/// business. Null means "do not render an open-now chip"; false means "shut right now".
/// </para>
/// <para>
/// <see cref="NextChangeAt"/> turns "Open now" into "Open now — closes at 20:00", which is
/// materially more useful and costs nothing extra once the schedule is loaded.
/// </para>
/// </summary>
public sealed record ProviderAvailabilityResponse
{
    /// <summary>The <c>AvailabilityKind</c> name — <c>BusinessHours</c>, <c>AppointmentOnly</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>An IANA zone. Every time in this response is wall-clock in it.</summary>
    public required string TimeZoneId { get; init; }

    /// <summary>True, false, or null when "open" is not a meaningful question.</summary>
    public bool? IsOpenNow { get; init; }

    /// <summary>When the answer next changes — closing time, or the next opening.</summary>
    public DateTimeOffset? NextChangeAt { get; init; }

    /// <summary>A phrase to render instead of Open/Closed. "By appointment", "Closed for Eid".</summary>
    public string? StatusText { get; init; }

    public required bool AcceptsWalkIns { get; init; }

    public required bool AcceptsAppointments { get; init; }

    public required bool OffersEmergencyService { get; init; }

    public required bool OffersRemoteService { get; init; }

    /// <summary>Free text beside the hours. "Closed for prayers 13:00–14:00".</summary>
    public string? Note { get; init; }

    /// <summary>Seven entries, Monday first, whether or not the provider has filled them in.</summary>
    public required IReadOnlyList<OperatingHoursResponse> OperatingHours { get; init; }

    /// <summary>Upcoming dated overrides. Past ones are swept and not returned.</summary>
    public required IReadOnlyList<AvailabilityExceptionResponse> Exceptions { get; init; }
}
