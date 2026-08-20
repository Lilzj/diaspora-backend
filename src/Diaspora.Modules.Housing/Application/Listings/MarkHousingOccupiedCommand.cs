using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Records that somebody has moved in, without going through a reservation.
/// <para>
/// The path for a landlord who let the property offline, which is common and must not be made
/// awkward: a platform that could only record lets it brokered would show a stale market and would
/// publish neighbourhood statistics computed from a fraction of the truth.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.ListingMarkOccupied)]
[Transactional(Reason = "Settles the listing units and writes the analytics record together.")]
[Auditable]
public sealed record MarkHousingOccupiedCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }

    public required MarkOccupiedRequest Request { get; init; }
}
