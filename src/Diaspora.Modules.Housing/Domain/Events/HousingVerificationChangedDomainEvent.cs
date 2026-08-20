using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when Trust's decision has been applied to a landlord or a property.
/// <para>
/// Raised by the outcome handler after the mark is written, never by a command. The distinction
/// matters: this event reports that Housing has caught up with Trust, not that Housing decided
/// anything.
/// </para>
/// </summary>
public sealed record HousingVerificationChangedDomainEvent : DomainEvent
{
    public required LandlordProfileId LandlordId { get; init; }

    public HousingListingId? ListingId { get; init; }

    public required HousingVerificationState PreviousState { get; init; }

    public required HousingVerificationState CurrentState { get; init; }

    public Guid? VerificationId { get; init; }

    /// <summary>Trust's badge, verbatim. Housing never invents one.</summary>
    public string? Badge { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
