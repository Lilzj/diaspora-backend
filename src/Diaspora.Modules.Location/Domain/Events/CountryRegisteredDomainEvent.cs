using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>Raised when a country enters the catalogue.</summary>
public sealed record CountryRegisteredDomainEvent : DomainEvent
{
    public required CountryId CountryId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required bool IsSupported { get; init; }
}
