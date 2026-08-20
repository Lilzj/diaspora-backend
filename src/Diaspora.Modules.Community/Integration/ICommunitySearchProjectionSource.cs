using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Community.Integration;

/// <summary>
/// The seam B15 will read to build its index.
/// <para>
/// Declared now and implemented now, with no search engine behind it, because the expensive part
/// of retrofitting search is never the engine — it is discovering that the content module cannot
/// produce a stable, paginated, incremental projection of its own corpus. This can, from the
/// first day.
/// </para>
/// <para>
/// Incremental by design: a caller passes the timestamp it last saw and gets what changed. A
/// full-corpus method would be the only one anybody used, and re-indexing everything nightly is
/// how a search index falls hours behind the content it indexes.
/// </para>
/// </summary>
public interface ICommunitySearchProjectionSource
{
    /// <summary>Content changed since a watermark, oldest first, cursor-paged.</summary>
    Task<CursorPagedResult<SearchProjection>> GetChangedAsync(
        DateTimeOffset since,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);
}
