using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Copies a property the caller's landlord profile owns into a new draft — BG-07.
/// <para>
/// The case that makes it worth building: a landlord listing several units in one building, or a
/// hostel listing rooms that differ only by number, who would otherwise retype the whole form each
/// time.
/// </para>
/// </summary>
[Authorize(Policy = HousingPolicies.ListingCreate)]
public sealed record DuplicateHousingListingCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }

    /// <summary>An optional title for the copy. Null takes the source's, prefixed.</summary>
    public string? Title { get; init; }
}
