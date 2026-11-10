using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Sellers;

/// <summary>
/// A seller's storefront: their profile and what they have for sale.
/// <para>
/// Unauthenticated. A buyer deciding whether to trust a seller reads this before they sign up, and
/// a storefront that required an account would be a shop with the shutters half down.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetStorefrontQuery : IQuery<SellerProfileResponse>
{
    public Guid? SellerId { get; init; }

    /// <summary>The storefront handle. Either this or the identifier.</summary>
    public string? Slug { get; init; }
}
