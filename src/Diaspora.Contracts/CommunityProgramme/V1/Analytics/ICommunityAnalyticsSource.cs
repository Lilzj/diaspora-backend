namespace Diaspora.Contracts.CommunityProgramme.V1.Analytics;

/// <summary>
/// One community's membership figures, read by the module that owns communities.
/// <para>
/// <b>Why a seam.</b> FRONTEND-PHASE15-GAP-003 asks for a community dataset. Analytics references no
/// module, and nothing it records is keyed by a community — the community dimension on its raw rows
/// carries organizations, not communities. The owning module answers from its own membership table
/// instead, and decides who may read the answer with its own capability rule.
/// </para>
/// <para>
/// <b>Counts and instants only.</b> No member identifier crosses this boundary.
/// </para>
/// </summary>
public interface ICommunityAnalyticsSource
{
    /// <summary>
    /// The figures for one community, or null when no community answers to
    /// <paramref name="idOrSlug"/>.
    /// <para>
    /// <paramref name="from"/> is inclusive and <paramref name="to"/> exclusive, both UTC.
    /// <paramref name="viewerIsPlatformAnalyst"/> is the caller's platform-wide analytics standing,
    /// which the owning module treats as staff standing; the module adds its own staff rule to it.
    /// When the viewer may not read the community's statistics the reading carries no figures.
    /// </para>
    /// </summary>
    Task<CommunityAnalyticsReading?> ReadAsync(
        string idOrSlug,
        Guid viewerUserId,
        bool viewerIsPlatformAnalyst,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

/// <summary>What the owning module reports for one community.</summary>
/// <param name="CommunityId">The community's identifier, resolved from the id or slug asked for.</param>
/// <param name="Name">The community's name, for the dataset's label.</param>
/// <param name="ViewerMayRead">
/// Whether the viewer may read this community's private statistics: its moderators, administrators
/// and owner, and staff. False carries no figures.
/// </param>
/// <param name="CurrentMembers">Joined members now (verified or not). Interest and following are not counted.</param>
/// <param name="JoinedAt">
/// When each membership that began inside the window began. A member who left and rejoined counts at
/// the latest join only, because that is the only one the membership row keeps.
/// </param>
/// <param name="EndedAt">When each membership that ended inside the window ended — left or removed.</param>
public sealed record CommunityAnalyticsReading(
    Guid CommunityId,
    string Name,
    bool ViewerMayRead,
    long CurrentMembers,
    IReadOnlyList<DateTimeOffset> JoinedAt,
    IReadOnlyList<DateTimeOffset> EndedAt);
