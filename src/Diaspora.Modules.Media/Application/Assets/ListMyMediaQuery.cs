using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// The caller's own media library, newest first.
/// <para>
/// Paged rather than complete: a member running a marketplace shop for two years has
/// thousands of photographs and a library screen renders twenty.
/// </para>
/// </summary>
public sealed record ListMyMediaQuery : IQuery<PagedResult<MediaAssetResponse>>
{
    /// <summary>Filter to one kind. Null for everything.</summary>
    public string? Kind { get; init; }

    public PaginationRequest Pagination { get; init; } = new();
}
