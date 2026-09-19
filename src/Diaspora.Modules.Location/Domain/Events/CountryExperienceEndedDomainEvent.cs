using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>
/// Raised when a residence gains or changes a departure date.
/// <para>
/// Distinct from a plain amendment because leaving a country is the event that ends a
/// member's standing in its community — which is a Trust concern, and Trust cannot act on a
/// fact it is not told. It was not told: this event had one producer and, until relocation was
/// wired, no consumer anywhere in the solution.
/// </para>
/// <para>
/// <b>The trigger is the date, not the transition.</b> It was originally raised only when an
/// <em>open</em> residence closed, which missed the member who declares a residence they have
/// already left — the common case, since nothing obliges anyone to fill in their timeline while
/// they are still living somewhere. Re-saving a residence whose departure date has not moved
/// raises nothing, so an edit to a role or an expertise claim does not announce a move.
/// </para>
/// </summary>
public sealed record CountryExperienceEndedDomainEvent : DomainEvent
{
    public required CountryExperienceId ExperienceId { get; init; }

    public required UserId UserId { get; init; }

    public required string CountryCode { get; init; }

    public required DateOnly DepartedOn { get; init; }
}
