using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Copies a listing the caller's seller profile owns into a new draft — BG-07.
/// <para>
/// The commonest real use is the one that makes duplication worth building at all: a seller listing
/// several near-identical items, who would otherwise retype the description, the category, the
/// fulfilment options and the location for each one.
/// </para>
/// </summary>
public sealed record DuplicateListingCommand : ICommand<ListingResponse>
{
    public required Guid ListingId { get; init; }

    /// <summary>An optional title for the copy. Null takes the source's, prefixed.</summary>
    public string? Title { get; init; }
}
