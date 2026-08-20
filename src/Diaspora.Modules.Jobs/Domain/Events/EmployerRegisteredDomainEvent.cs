using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>Raised when an employer profile is created.</summary>
public sealed record EmployerRegisteredDomainEvent : DomainEvent
{
    public required EmployerProfileId EmployerId { get; init; }

    public required UserId OwnerId { get; init; }

    public required string DisplayName { get; init; }

    public required string Slug { get; init; }

    public required EmployerOrganizationType OrganizationType { get; init; }

    public required string CountryCode { get; init; }
}
