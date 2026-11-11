using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Offers;

/// <summary>
/// One negotiation in full, with every round of it.
/// <para>
/// Readable only by its two parties. Staff have no bypass here — reading a named person's
/// negotiation is a different act from moderating a public listing, and it should require a
/// deliberate support tool rather than fall out of a role check.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.OfferRespond)]
public sealed record GetOfferQuery : IQuery<OfferResponse>
{
    public required Guid OfferId { get; init; }
}
