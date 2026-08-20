using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>
/// Raised when an open residence acquires a departure date.
/// <para>
/// Distinct from a plain amendment because leaving a country is the event that ends a
/// member's standing in its community — which is a Trust concern, and Trust cannot act on a
/// fact it is not told.
/// </para>
/// </summary>
public sealed record CountryExperienceEndedDomainEvent : DomainEvent
{
    public required CountryExperienceId ExperienceId { get; init; }

    public required UserId UserId { get; init; }

    public required string CountryCode { get; init; }

    public required DateOnly DepartedOn { get; init; }
}
