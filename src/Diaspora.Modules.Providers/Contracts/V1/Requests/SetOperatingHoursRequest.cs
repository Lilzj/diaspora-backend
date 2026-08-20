namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Replaces a provider's whole weekly schedule, and its availability mode.</summary>
public sealed record SetOperatingHoursRequest
{
    /// <summary>The <c>AvailabilityKind</c> name. Defaults to <c>BusinessHours</c>.</summary>
    public string? Kind { get; init; }

    /// <summary>
    /// An IANA zone — "Europe/London", "Indian/Mauritius". Rejected rather than defaulted when
    /// unrecognised: silently falling back to UTC produces hours that are wrong by a whole number
    /// of hours and look entirely plausible.
    /// </summary>
    public string? TimeZoneId { get; init; }

    public bool AcceptsWalkIns { get; init; }

    public bool AcceptsAppointments { get; init; }

    public bool OffersEmergencyService { get; init; }

    public bool OffersRemoteService { get; init; }

    /// <summary>Free text beside the hours. "Closed for prayers 13:00–14:00".</summary>
    public string? Note { get; init; }

    /// <summary>Days not mentioned are left as they are. An empty list changes only the mode.</summary>
    public IReadOnlyList<OperatingHoursRequest> Days { get; init; } = [];
}
