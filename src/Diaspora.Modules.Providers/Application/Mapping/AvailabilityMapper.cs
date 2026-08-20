using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Availability;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Extensibility.Availability;

namespace Diaspora.Modules.Providers.Application.Mapping;

/// <summary>
/// Turns a provider's schedule into the shape the wire reads.
/// <para>
/// <b>All seven days are always returned</b>, whether or not the provider has filled them in,
/// with <c>isSpecified</c> saying which is which. A client rendering a week of rows should not
/// have to reconcile a sparse list against a calendar, and one that did would render Monday
/// where Wednesday belongs the first time a provider left a day blank.
/// </para>
/// <para>
/// Past exceptions are dropped rather than returned. A closure from last Christmas is not
/// information; it is noise on a screen a provider is trying to edit.
/// </para>
/// </summary>
public static class AvailabilityMapper
{
    /// <summary>Monday first, matching how the client's hours editor is laid out.</summary>
    private static readonly DayOfWeek[] WeekOrder =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    public static ProviderAvailabilityResponse ToResponse(
        Provider provider, AvailabilityAnswer answer, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(answer);

        var byDay = provider.OperatingHours.ToDictionary(hours => hours.Day);

        return new ProviderAvailabilityResponse
        {
            Kind = ProviderWireNames.ToWire(provider.Availability.Kind),
            TimeZoneId = provider.Availability.TimeZoneId,
            IsOpenNow = answer.IsOpen,
            NextChangeAt = answer.NextChangeAt,
            StatusText = answer.Reason,
            AcceptsWalkIns = provider.Availability.AcceptsWalkIns,
            AcceptsAppointments = provider.Availability.AcceptsAppointments,
            OffersEmergencyService = provider.Availability.OffersEmergencyService,
            OffersRemoteService = provider.Availability.OffersRemoteService,
            Note = provider.Availability.Note,
            OperatingHours = WeekOrder
                .Select(day => byDay.TryGetValue(day, out var hours)
                    ? ToResponse(hours)
                    : Unspecified(day))
                .ToList(),
            Exceptions = provider.AvailabilityExceptions
                .Where(exception => !exception.HasLapsed(today))
                .Select(ToResponse)
                .ToList()
        };
    }

    public static OperatingHoursResponse ToResponse(OperatingHours hours)
    {
        ArgumentNullException.ThrowIfNull(hours);

        return new OperatingHoursResponse
        {
            DayOfWeek = ProviderWireNames.ToWire(hours.Day),
            IsClosed = hours.IsClosed,
            IsSpecified = hours.IsSpecified,
            Windows = hours.Windows.Select(ToResponse).ToList(),
            DisplayText = hours.Describe()
        };
    }

    public static AvailabilityWindowResponse ToResponse(AvailabilityWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        return new AvailabilityWindowResponse
        {
            WindowId = window.Id.Value,
            OpensAt = Format(window.OpensAt),
            ClosesAt = Format(window.ClosesAt),
            Label = window.Label,
            CrossesMidnight = window.CrossesMidnight
        };
    }

    public static AvailabilityExceptionResponse ToResponse(AvailabilityException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new AvailabilityExceptionResponse
        {
            ExceptionId = exception.Id.Value,
            Kind = ProviderWireNames.ToWire(exception.Kind),
            StartsOn = exception.StartsOn,
            EndsOn = exception.EndsOn,
            IsClosed = exception.IsClosed,
            OpensAt = exception.OpensAt is { } opens ? Format(opens) : null,
            ClosesAt = exception.ClosesAt is { } closes ? Format(closes) : null,
            Reason = exception.Reason
        };
    }

    /// <summary>
    /// A day the provider has said nothing about. Not closed — silent, which a client should
    /// render as nothing rather than as a closure the provider never declared.
    /// </summary>
    private static OperatingHoursResponse Unspecified(DayOfWeek day) => new()
    {
        DayOfWeek = ProviderWireNames.ToWire(day),
        IsClosed = false,
        IsSpecified = false,
        Windows = [],
        DisplayText = string.Empty
    };

    private static string Format(TimeOnly time) =>
        time.ToString("HH\\:mm", System.Globalization.CultureInfo.InvariantCulture);
}
