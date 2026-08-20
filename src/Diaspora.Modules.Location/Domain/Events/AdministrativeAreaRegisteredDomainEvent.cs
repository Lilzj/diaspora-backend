using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>Raised when a node joins the geographic hierarchy.</summary>
public sealed record AdministrativeAreaRegisteredDomainEvent : DomainEvent
{
    public required AreaId AreaId { get; init; }

    public required AdministrativeLevel Level { get; init; }

    public required string Name { get; init; }

    public required string CountryCode { get; init; }

    public required string Path { get; init; }
}
