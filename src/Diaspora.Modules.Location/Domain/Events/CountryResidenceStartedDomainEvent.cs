using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>
/// Raised when a residence becomes the member's current one — 21 Oct 2026.
/// <para>
/// The counterpart to <see cref="CountryExperienceEndedDomainEvent"/>, and the half relocation never
/// had: leaving a country was told to the platform, arriving in one was not, so nothing on the server
/// could act on the country a member is in now (<c>location.countryChanged</c>).
/// </para>
/// <para>
/// <b>Raised on the transition to an open residence, not on every save.</b> Declaring a residence
/// with no departure date, or reopening one that had ended (the member came back), raises it.
/// Re-saving a current residence to change a role or an arrival date does not, and nor does
/// declaring a residence already left — that is history, not where they are.
/// </para>
/// </summary>
public sealed record CountryResidenceStartedDomainEvent : DomainEvent
{
    public required CountryExperienceId ExperienceId { get; init; }

    public required UserId UserId { get; init; }

    public required string CountryCode { get; init; }

    public required DateOnly ArrivedOn { get; init; }
}
