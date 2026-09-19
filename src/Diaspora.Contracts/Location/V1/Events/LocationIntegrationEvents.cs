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
