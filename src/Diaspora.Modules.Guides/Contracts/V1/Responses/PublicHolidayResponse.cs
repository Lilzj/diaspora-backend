namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// A day the offices are shut, and what that means for somebody with an appointment.
/// <para>
/// <b>The two closure flags are the payload; the name is the label.</b> "Independence Day" is a fact
/// about a country. "The passport office is shut" is the fact that changes what a member does this
/// week, and the two do not always travel together — several religious holidays close nothing, and
/// several missions close on their home country's holidays rather than the local ones.
/// </para>
/// </summary>
public sealed record PublicHolidayResponse
{
    public required Guid PublicHolidayId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Already localized. Holiday names differ across communities rather than translate.</summary>
    public required string Name { get; init; }

    /// <inheritdoc cref="Name" />
    public string? Description { get; init; }

    /// <summary>PascalCase — <c>National</c>, <c>Religious</c>, <c>EmbassyClosure</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The day it falls on. Date-only in meaning; midnight UTC on the wire.</summary>
    public required DateTimeOffset ObservedOn { get; init; }

    /// <summary>The last day of a multi-day observance, or the same day as a single one.</summary>
    public required DateTimeOffset ObservedUntil { get; init; }

    public required int Year { get; init; }

    /// <summary>The area it applies to, or null for nationwide.</summary>
    public string? Region { get; init; }

    public required bool ClosesGovernmentOffices { get; init; }

    public required bool ClosesEmbassies { get; init; }

    /// <summary>The locales a translation exists for.</summary>
    public IReadOnlyList<string> AvailableLocales { get; init; } = [];

    /// <summary>The concurrency token to send back on an edit. Editors only.</summary>
    public int? Revision { get; init; }
}
