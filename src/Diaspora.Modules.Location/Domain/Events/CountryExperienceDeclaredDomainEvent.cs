using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>
/// Raised when a member declares that they have lived in a country.
/// <para>
/// A claim, not a verified fact. Consumers that need proof must join
/// <see cref="Residency.VerifiedResidency"/> or listen to Trust directly — this event says
/// only that someone said so.
/// </para>
/// </summary>
public sealed record CountryExperienceDeclaredDomainEvent : DomainEvent
{
    public required CountryExperienceId ExperienceId { get; init; }

    public required UserId UserId { get; init; }

    public required string CountryCode { get; init; }

    public required DateOnly ArrivedOn { get; init; }

    public DateOnly? DepartedOn { get; init; }
}
