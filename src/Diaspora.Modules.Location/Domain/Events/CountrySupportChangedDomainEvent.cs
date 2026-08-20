using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>
/// Raised when the platform opens or closes in a country.
/// <para>
/// The one country event other modules genuinely need: a marketplace that lists in a country
/// the platform has withdrawn from is a compliance problem, not a stale cache.
/// </para>
/// </summary>
public sealed record CountrySupportChangedDomainEvent : DomainEvent
{
    public required CountryId CountryId { get; init; }

    public required string Code { get; init; }

    public required bool IsSupported { get; init; }

    public required string Reason { get; init; }
}
