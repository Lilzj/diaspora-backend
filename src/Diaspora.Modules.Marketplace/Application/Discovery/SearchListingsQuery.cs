using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Discovery;

/// <summary>
/// Searches and browses listings.
/// <para>
/// Unauthenticated, like every discovery surface in this module: a marketplace nobody can look at
/// before signing up is one nobody signs up for.
/// </para>
/// </summary>
public sealed record SearchListingsQuery : IQuery<ListingSearchResultResponse>
{
    public required SearchListingsRequest Request { get; init; }
}
