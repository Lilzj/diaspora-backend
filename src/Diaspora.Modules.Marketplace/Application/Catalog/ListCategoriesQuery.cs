using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Catalog;

/// <summary>
/// The commerce taxonomy.
/// <para>
/// Read whole rather than paged: it is a few hundred rows that change when code ships, and it is
/// the first thing every client asks for. Sent flat with materialised paths, so the client builds
/// whatever shape its screen needs.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListCategoriesQuery : IQuery<IReadOnlyList<ListingCategoryResponse>>
{
    /// <summary>Include retired nodes. Staff surfaces only, in practice.</summary>
    public bool IncludeInactive { get; init; }
}
