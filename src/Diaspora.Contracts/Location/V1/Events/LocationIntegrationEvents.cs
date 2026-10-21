using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Location.V1.Events;

/// <summary>
/// A member closed an open residence: they have left that country.
/// <para>
/// <b>Why this contract exists.</b> Location has held a member's residence history since B4,
/// and <c>CountryExperienceEndedDomainEvent</c> has said in its own summary since then that
/// leaving a country "is a Trust concern, and Trust cannot act on a fact it is not told".
/// Nothing ever told it. The domain event had one producer and no consumer, and
/// <c>CountryStanding.RecordDeparture</c> — the method on the other side, written to receive
/// exactly this — had no caller at all. Both halves of relocation were built; the wire between
/// them was not, so a member who moved stayed a verified resident of the country they left, for
/// ever.
/// </para>
/// <para>
/// <b>Declared, not proven.</b> This carries what the member says about themselves. Trust may
/// close a standing on it because closing one takes nothing away — the points, the level and the
/// verification all survive a departure, and <c>DepartedOn</c> only stops the residence clock
/// that <c>MinimumResidenceYears</c> reads. Nothing here may ever grant anything.
/// </para>
/// </summary>
public sealed record CountryResidenceEndedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The day the member says they left.</summary>
    public required DateOnly DepartedOn { get; init; }
}

/// <summary>
/// A member's residence started in a country: this is the country they live in now — 21 Oct 2026.
/// <para>
/// <b>The half relocation never published.</b> <see cref="CountryResidenceEndedIntegrationEvent"/>
/// names the country left; this names the country entered, which is what
/// <c>location.countryChanged</c>'s country condition is about. Published when a residence with no
/// departure date is declared, or when one that had ended is reopened — never on a re-save of the
/// current one, and never for a residence declared as already left.
/// </para>
/// <para>
/// <b>Declared, not proven</b>, exactly as its counterpart: the member says where they live. Nothing
/// may grant anything on it; it may tell the member themselves something.
/// </para>
/// </summary>
public sealed record CountryResidenceStartedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The country's name for a sentence, or the code when the catalogue has none.</summary>
    public required string CountryName { get; init; }

    /// <summary>The day the member says they arrived.</summary>
    public required DateOnly ArrivedOn { get; init; }
}

/// <summary>
/// A member who opted in to "when the app is open" location entered an area somebody's automation
/// watches — 21 Oct 2026, the contract behind <c>location.enteredArea</c>.
/// <para>
/// <b>No coordinates, ever.</b> The device reported a point while the app was in the foreground;
/// Location resolved it to a catalogue area and kept only that area's identifier, and this carries
/// only areas. <see cref="EnteredAreaIds"/> is every area the member is now inside and was not before
/// — the most specific first, then the town or district that holds it when that is new too — so an
/// automation watching a town fires on arriving in the town, and not on walking between two of its
/// neighbourhoods.
/// </para>
/// <para>
/// Published only when one of those areas is watched by an active automation of this member's (or
/// they have one that watches any area), so a member nobody's rule cares about leaves nothing on the
/// bus.
/// </para>
/// </summary>
public sealed record MemberEnteredAreaIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    /// <summary>The most specific catalogue area the member is now in.</summary>
    public required Guid AreaId { get; init; }

    public required string AreaName { get; init; }

    /// <summary><c>Neighborhood</c>, <c>City</c>, <c>District</c> and so on.</summary>
    public required string AreaLevel { get; init; }

    /// <summary>ISO 3166-1 alpha-2 of the area, from the catalogue.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Every area newly entered, most specific first. Always contains <see cref="AreaId"/>.</summary>
    public required IReadOnlyList<Guid> EnteredAreaIds { get; init; }

    public required DateTimeOffset EnteredAt { get; init; }
}
