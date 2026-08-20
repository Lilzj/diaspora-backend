namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Adds a mission to the embassy directory.
/// <para>
/// Neither country code can be changed afterwards. A mission that moved country is a different
/// mission, and one that started serving a different nationality is a diplomatic event rather than a
/// typo — both are a new record and an archived old one, which is also the only version of the story
/// that leaves the old entry findable.
/// </para>
/// </summary>
public sealed record CreateEmbassyRequest
{
    /// <summary>Upper-case ISO 3166-1 alpha-2 — where the mission is located.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — whose nationals it serves.</summary>
    public required string ServesCountryCode { get; init; }

    public required string Name { get; init; }

    public string? City { get; init; }

    public string? Address { get; init; }
}

/// <summary>
/// Everything about a mission an editor may rewrite.
/// <para>
/// One request rather than six, for the reason <c>UpdateOfficialNoticeRequest</c> gives: one editing
/// screen holds all of it and saves once. Collections supplied are replaced wholesale; collections
/// absent are untouched.
/// </para>
/// </summary>
public sealed record UpdateEmbassyRequest
{
    public required string Name { get; init; }

    public string? City { get; init; }

    public string? Address { get; init; }

    /// <summary>Out-of-range coordinates are dropped rather than refused — a bad pin is not a bad entry.</summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? Telephone { get; init; }

    /// <summary>The out-of-hours line. The most consequential field in this module.</summary>
    public string? EmergencyTelephone { get; init; }

    public string? Email { get; init; }

    public string? Website { get; init; }

    public string? AppointmentUrl { get; init; }

    public bool RequiresAppointment { get; init; }

    // ── The four every directory filter reads ───────────────────────────────────────────────

    public bool OffersPassportServices { get; init; }

    public bool OffersVisaServices { get; init; }

    public bool OffersConsularServices { get; init; }

    public bool OffersEmergencyAssistance { get; init; }

    /// <summary>A B5 media reference for the building photograph. <c>Guid.Empty</c> clears it.</summary>
    public Guid? MediaId { get; init; }

    /// <summary>The issuing authority record for this mission. Absent leaves the link alone.</summary>
    public Guid? AuthorityId { get; init; }

    /// <summary>Whether <see cref="AuthorityId"/> is being set at all, since null means "unlink".</summary>
    public bool SetAuthority { get; init; }

    /// <summary>Supplied means replace; absent means leave.</summary>
    public IReadOnlyList<OfficeHoursInput>? OfficeHours { get; init; }

    /// <inheritdoc cref="OfficeHours" />
    public IReadOnlyList<EmbassyServiceInput>? Services { get; init; }

    /// <inheritdoc cref="OfficeHours" />
    public IReadOnlyList<EmbassyLocalizationInput>? Localizations { get; init; }

    public int? ExpectedRevision { get; init; }
}

/// <summary>When a mission is open on one day.</summary>
public sealed record OfficeHoursInput
{
    /// <summary>PascalCase day name — <c>Monday</c>.</summary>
    public required string Day { get; init; }

    /// <summary>Free text, because real missions publish free text. Absent needs <c>IsClosed</c>.</summary>
    public string? Hours { get; init; }

    public bool IsClosed { get; init; }
}

/// <summary>One thing a mission does, beyond the four headline capabilities.</summary>
public sealed record EmbassyServiceInput
{
    /// <summary>Lower-case kebab. Unique within the mission.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public bool RequiresAppointment { get; init; }
}

/// <summary>A mission's name, description and address in one locale.</summary>
public sealed record EmbassyLocalizationInput
{
    public required string Locale { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Translated too — an address a reader cannot show a taxi driver is not an address.</summary>
    public string? Address { get; init; }
}
