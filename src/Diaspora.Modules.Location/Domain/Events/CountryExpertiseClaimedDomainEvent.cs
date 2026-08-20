using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>Raised when a member claims expertise in a subject for a country.</summary>
public sealed record CountryExpertiseClaimedDomainEvent : DomainEvent
{
    public required CountryExperienceId ExperienceId { get; init; }

    public required UserId UserId { get; init; }

    public required string CountryCode { get; init; }

    public required string Area { get; init; }
}
