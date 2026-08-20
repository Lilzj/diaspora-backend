using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a landlord or a property is submitted to Trust.
/// <para>
/// A request, never a decision. Nothing in Housing may raise an event that says something was
/// verified - that fact arrives on Trust's own published outcome and is applied by the outcome
/// handler.
/// </para>
/// </summary>
public sealed record HousingVerificationRequestedDomainEvent : DomainEvent
{
    public required LandlordProfileId LandlordId { get; init; }

    /// <summary>Set when the claim is about one property rather than the landlord.</summary>
    public HousingListingId? ListingId { get; init; }

    public required UserId RequestedBy { get; init; }

    /// <summary>Trust's case identifier.</summary>
    public required Guid VerificationId { get; init; }
}
