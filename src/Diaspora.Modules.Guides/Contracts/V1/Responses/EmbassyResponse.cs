namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One diplomatic mission in the embassy directory.
/// <para>
/// <b>The emergency line is its own field, and that is not a formatting preference.</b> Somebody who
/// needs it is not going to read a paragraph to find it, and a client renders it as its own control
/// for exactly that reason. It is the single most consequential field this module serves.
/// </para>
/// <para>
/// <b>Two country codes, both present, neither derivable from the other.</b> Where the mission is,
/// and whose nationals it serves.
/// </para>
/// </summary>
public sealed record EmbassyResponse
{
    public required Guid EmbassyId { get; init; }

    /// <summary>Already localized, with the usual exact→language→default fallback.</summary>
    public required string Name { get; init; }

    /// <inheritdoc cref="Name" />
    public string? Description { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — where the mission is located.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — whose nationals it serves.</summary>
    public required string ServesCountryCode { get; init; }

    public string? City { get; init; }

    /// <inheritdoc cref="Name" />
    public string? Address { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? Telephone { get; init; }

    /// <summary>The out-of-hours line. Rendered as its own control.</summary>
    public string? EmergencyTelephone { get; init; }

    public string? Email { get; init; }

    public string? Website { get; init; }

    public string? AppointmentUrl { get; init; }

    public required bool RequiresAppointment { get; init; }

    // ── The four every directory filter reads ───────────────────────────────────────────────

    public required bool OffersPassportServices { get; init; }

    public required bool OffersVisaServices { get; init; }

    public required bool OffersConsularServices { get; init; }

    public required bool OffersEmergencyAssistance { get; init; }

    /// <summary>A B5 media reference for the building photograph. A reference, never a URL.</summary>
    public Guid? MediaId { get; init; }

    /// <summary>The issuing authority record for this mission, when one exists.</summary>
    public Guid? AuthorityId { get; init; }

    public required bool IsVerified { get; init; }

    public required bool IsActive { get; init; }

    public IReadOnlyList<EmbassyOfficeHoursResponse> OfficeHours { get; init; } = [];

    public IReadOnlyList<EmbassyServiceResponse> Services { get; init; } = [];

    /// <summary>The locales a translation exists for.</summary>
    public IReadOnlyList<string> AvailableLocales { get; init; } = [];

    /// <summary>The concurrency token to send back on an edit. Editors only.</summary>
    public int? Revision { get; init; }
}

/// <summary>
/// When a mission is open on one day.
/// <para>
/// The text is free-form because real missions publish "09:00–12:00, closed for visa collection after
/// 11:00" and "by appointment only". <see cref="IsClosed"/> is the one structured part, because it is
/// the one thing a client renders as a state rather than a sentence.
/// </para>
/// </summary>
public sealed record EmbassyOfficeHoursResponse
{
    /// <summary>PascalCase day name — <c>Monday</c>.</summary>
    public required string Day { get; init; }

    public string? Hours { get; init; }

    public required bool IsClosed { get; init; }
}

/// <summary>One thing a mission does, beyond the four headline capabilities.</summary>
public sealed record EmbassyServiceResponse
{
    public required string Key { get; init; }

    /// <summary>Already localized where a translation exists.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Per service, because a mission that takes walk-ins for one and not another is normal.</summary>
    public required bool RequiresAppointment { get; init; }
}
