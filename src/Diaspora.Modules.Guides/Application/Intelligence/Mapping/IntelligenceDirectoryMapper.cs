using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Embassies;
using Diaspora.Modules.Guides.Domain.Emergency;
using Diaspora.Modules.Guides.Domain.Holidays;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Visas;

namespace Diaspora.Modules.Guides.Application.Intelligence.Mapping;

/// <summary>
/// Turns missions, emergency directories, visa advisories and calendar entries into what a client
/// renders.
/// <para>
/// Static, like every mapper in this module. See <see cref="OfficialNoticeMapper"/> for why.
/// </para>
/// </summary>
public static class IntelligenceDirectoryMapper
{
    public static EmbassyResponse ToResponse(
        Embassy embassy, string? locale, bool forEditor = false)
    {
        ArgumentNullException.ThrowIfNull(embassy);

        var (name, description, address) = embassy.Localized(locale);

        return new EmbassyResponse
        {
            EmbassyId = embassy.Id.Value,
            Name = name,
            Description = description,
            CountryCode = embassy.CountryCode,
            ServesCountryCode = embassy.ServesCountryCode,
            City = embassy.City,
            Address = address,
            Latitude = embassy.Latitude,
            Longitude = embassy.Longitude,
            Telephone = embassy.Telephone,
            EmergencyTelephone = embassy.EmergencyTelephone,
            Email = embassy.Email,
            Website = embassy.Website,
            AppointmentUrl = embassy.AppointmentUrl,
            RequiresAppointment = embassy.RequiresAppointment,
            OffersPassportServices = embassy.OffersPassportServices,
            OffersVisaServices = embassy.OffersVisaServices,
            OffersConsularServices = embassy.OffersConsularServices,
            OffersEmergencyAssistance = embassy.OffersEmergencyAssistance,
            MediaId = embassy.MediaId,
            AuthorityId = embassy.AuthorityId?.Value,
            IsVerified = embassy.IsVerified,
            IsActive = embassy.IsActive,
            OfficeHours =
            [
                .. embassy.OfficeHours
                    .OrderBy(hours => hours.Day)
                    .Select(hours => new EmbassyOfficeHoursResponse
                    {
                        Day = hours.Day.ToString(),
                        Hours = hours.Hours,
                        IsClosed = hours.IsClosed
                    })
            ],
            Services =
            [
                .. embassy.Services
                    .OrderBy(service => service.Position)
                    .Select(service => new EmbassyServiceResponse
                    {
                        Key = service.Key,
                        Name = service.Name,
                        Description = service.Description,
                        RequiresAppointment = service.RequiresAppointment
                    })
            ],
            AvailableLocales =
                [.. embassy.Localizations.Select(localization => localization.Locale).Order()],
            Revision = forEditor ? embassy.Revision : null
        };
    }

    /// <summary>
    /// A country's emergency information, every label and note resolved to the caller's locale.
    /// <para>
    /// The localization is applied per contact through the aggregate, because the fallback ladder
    /// lives there and a second implementation here would be the two drifting.
    /// </para>
    /// </summary>
    public static EmergencyContactsResponse ToResponse(
        CountryEmergencyDirectory directory, string? locale, bool forEditor = false)
    {
        ArgumentNullException.ThrowIfNull(directory);

        return new EmergencyContactsResponse
        {
            CountryCode = directory.CountryCode,
            Guidance = directory.Guidance,
            Contacts =
            [
                .. directory.Contacts
                    .OrderBy(contact => contact.Position)
                    .Select(contact =>
                    {
                        var (label, note) = directory.Localized(contact, locale);

                        return new EmergencyContactResponse
                        {
                            Kind = contact.Kind,
                            Label = label,
                            Number = contact.Number,
                            AlternateNumber = contact.AlternateNumber,
                            Note = note,
                            IsAvailableAllHours = contact.IsAvailableAllHours,
                            Position = contact.Position
                        };
                    })
            ],
            LastReviewedAt = directory.LastReviewedAt,
            ResolvedLocale = IntelligenceNormalization.Locale(locale) ?? string.Empty,
            Revision = forEditor ? directory.Revision : null
        };
    }

    public static VisaAdvisoryResponse ToResponse(
        VisaAdvisory advisory,
        IssuingAuthority? authority,
        DateTimeOffset asOf,
        string? locale,
        bool forEditor = false)
    {
        ArgumentNullException.ThrowIfNull(advisory);

        var (title, summary, content) = advisory.Localized(locale);

        return new VisaAdvisoryResponse
        {
            VisaAdvisoryId = advisory.Id.Value,
            CountryCode = advisory.CountryCode,
            AppliesToCountryCode = advisory.AppliesToCountryCode,
            Title = title,
            Summary = summary,
            Content = content,
            VisaCategory = advisory.VisaCategory,
            Status = IntelligenceWireNames.ToWire(advisory.Status),
            AuthorityId = advisory.AuthorityId.Value,
            AuthorityName = authority?.Name ?? string.Empty,
            IsOfficial = authority?.IsOfficial ?? false,
            PostedByPlatform = authority?.IsPlatform ?? false,
            Requirements = [.. advisory.Requirements.Select(line => line.Text)],
            Documents = [.. advisory.Documents.Select(line => line.Text)],
            ProcessingTime = advisory.ProcessingTime,
            ApplicationUrl = advisory.ApplicationUrl,
            OfficialReference = advisory.OfficialReference,
            SourceUrl = advisory.SourceUrl,
            EffectiveFrom = advisory.EffectiveFrom,
            ExpiresAt = advisory.ExpiresAt,
            PublishedAt = advisory.PublishedAt,
            LastReviewedAt = advisory.LastReviewedAt,
            IsActive = advisory.IsActiveAt(asOf),
            RelatedGuideIds =
            [
                .. advisory.RelatedGuides
                    .OrderBy(reference => reference.Position)
                    .Select(reference => reference.GuideId)
            ],
            AvailableLocales =
                [.. advisory.Localizations.Select(localization => localization.Locale).Order()],
            Revision = forEditor ? advisory.Revision : null
        };
    }

    public static PublicHolidayResponse ToResponse(
        PublicHoliday holiday, string? locale, bool forEditor = false)
    {
        ArgumentNullException.ThrowIfNull(holiday);

        var (name, description) = holiday.Localized(locale);

        return new PublicHolidayResponse
        {
            PublicHolidayId = holiday.Id.Value,
            CountryCode = holiday.CountryCode,
            Name = name,
            Description = description,
            Kind = IntelligenceWireNames.ToWire(holiday.Kind),
            ObservedOn = holiday.ObservedOn,
            ObservedUntil = holiday.ObservedUntil,
            Year = holiday.Year,
            Region = holiday.Region,
            ClosesGovernmentOffices = holiday.ClosesGovernmentOffices,
            ClosesEmbassies = holiday.ClosesEmbassies,
            AvailableLocales =
                [.. holiday.Localizations.Select(localization => localization.Locale).Order()],
            Revision = forEditor ? holiday.Revision : null
        };
    }
}
