using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>Takes a property off the market, temporarily or for good.</summary>
[Authorize(Permission = HousingPermissions.ListingArchive)]
[Auditable]
public sealed record ArchiveHousingListingCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public ArchiveHousingListingRequest? Request { get; init; }
}
