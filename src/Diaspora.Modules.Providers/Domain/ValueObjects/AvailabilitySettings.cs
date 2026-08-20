using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.ValueObjects;

/// <summary>
/// The provider-wide facts an open-now calculation needs before it looks at any day.
/// <para>
/// <b><see cref="TimeZoneId"/> is the field this type exists for.</b> "Open now" is a question
/// about the provider's local time, and the server's is irrelevant — a Mauritius caterer is
/// open at 09:00 MUT, which is 05:00 UTC, and a calculation that compares a stored
/// <c>09:00</c> against <c>DateTimeOffset.UtcNow</c> shows every provider east of London as
/// closed for four hours every morning. The client's mock data sidesteps this by hard-coding
/// <c>isOpenNow</c>; the server cannot.
/// </para>
/// <para>
/// The zone is an IANA identifier, validated on the way in. An invalid zone is rejected rather
/// than defaulted, because silently falling back to UTC produces a provider whose hours are
/// wrong by a whole number of hours and looks entirely plausible.
/// </para>
/// </summary>
public sealed class AvailabilitySettings : ValueObject
{
    public const int MaxTimeZoneLength = 64;

    public const int MaxNoteLength = 500;

    private AvailabilitySettings(
        AvailabilityKind kind,
        string timeZoneId,
        bool acceptsWalkIns,
        bool acceptsAppointments,
        bool offersEmergencyService,
        bool offersRemoteService,
        string? note)
    {
        Kind = kind;
        TimeZoneId = timeZoneId;
        AcceptsWalkIns = acceptsWalkIns;
        AcceptsAppointments = acceptsAppointments;
        OffersEmergencyService = offersEmergencyService;
        OffersRemoteService = offersRemoteService;
        Note = note;
    }

    private AvailabilitySettings() { }

    public AvailabilityKind Kind { get; private set; } = AvailabilityKind.BusinessHours;

    /// <summary>An IANA zone — "Europe/London", "Indian/Mauritius". Never a UTC offset.</summary>
    public string TimeZoneId { get; private set; } = DefaultTimeZoneId;

    /// <summary>The client's "Walk-ins welcome", which today lives in a free-text tag.</summary>
    public bool AcceptsWalkIns { get; private set; }

    public bool AcceptsAppointments { get; private set; }

    /// <summary>Reachable out of hours for urgent work.</summary>
    public bool OffersEmergencyService { get; private set; }

    /// <summary>Will serve a member who cannot attend in person.</summary>
    public bool OffersRemoteService { get; private set; }

    /// <summary>Free text the provider wants shown beside the hours. "Closed for prayers 13:00–14:00".</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Whether "open now" is a meaningful question at all. False for appointment-only and
    /// by-arrangement providers, whose clients should read "By appointment" and not "Closed".
    /// </summary>
    public bool HasOpenNowSemantics =>
        Kind is AvailabilityKind.BusinessHours or AvailabilityKind.AlwaysOpen or AvailabilityKind.Unavailable;

    public const string DefaultTimeZoneId = "UTC";

    public static AvailabilitySettings Default =>
        new(AvailabilityKind.BusinessHours, DefaultTimeZoneId, true, false, false, false, null);

    /// <summary>
    /// Builds settings, resolving the time zone against the host's database.
    /// <para>
    /// Returns null on an unknown zone rather than falling back, so the caller decides. Every
    /// caller in this module turns that into <c>providers.availability.timezone-invalid</c>.
    /// </para>
    /// </summary>
    public static AvailabilitySettings? Create(
        AvailabilityKind kind,
        string? timeZoneId,
        bool acceptsWalkIns = false,
        bool acceptsAppointments = false,
        bool offersEmergencyService = false,
        bool offersRemoteService = false,
        string? note = null)
    {
        var zone = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim();

        if (zone.Length > MaxTimeZoneLength || !IsKnownTimeZone(zone))
        {
            return null;
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (trimmedNote is { Length: > MaxNoteLength })
        {
            trimmedNote = trimmedNote[..MaxNoteLength];
        }

        return new AvailabilitySettings(
            kind,
            zone,
            acceptsWalkIns,
            acceptsAppointments || kind == AvailabilityKind.AppointmentOnly,
            offersEmergencyService || kind == AvailabilityKind.EmergencyService,
            offersRemoteService,
            trimmedNote);
    }

    /// <summary>The provider's own local time, for an open-now calculation.</summary>
    public DateTimeOffset ToLocal(DateTimeOffset instant)
    {
        try
        {
            return TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));
        }
        catch (TimeZoneNotFoundException)
        {
            // A zone that validated at write time and cannot be found at read time means the
            // host's tz database moved under us. Falling back to UTC keeps the screen rendering;
            // failing the whole provider read would not.
            return instant.ToUniversalTime();
        }
        catch (InvalidTimeZoneException)
        {
            return instant.ToUniversalTime();
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return TimeZoneId;
        yield return AcceptsWalkIns;
        yield return AcceptsAppointments;
        yield return OffersEmergencyService;
        yield return OffersRemoteService;
        yield return Note;
    }

    private static bool IsKnownTimeZone(string zone)
    {
        if (string.Equals(zone, DefaultTimeZoneId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(zone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
