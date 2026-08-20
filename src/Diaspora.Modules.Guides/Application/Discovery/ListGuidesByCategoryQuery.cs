using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Everything published under one catalogue category.
/// <para>
/// Filed by <em>key</em> rather than by identifier, because the key is the contract: it is what a
/// contributor seed declares, what a URL carries and what a client switches on, and it survives a
/// reseed where a generated identifier would not.
/// </para>
/// </summary>
public sealed record ListGuidesByCategoryQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    public required string CategoryKey { get; init; }

    /// <summary>Narrows within the category. Null browses every country.</summary>
    public string? CountryCode { get; init; }

    public string? Language { get; init; }

    /// <summary>A sort key. Unknown values fall back to newest.</summary>
    public string? Sort { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
