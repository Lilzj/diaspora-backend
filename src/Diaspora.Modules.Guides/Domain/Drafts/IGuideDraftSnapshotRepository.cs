using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Domain.Drafts;

/// <summary>Reads and writes auto-save snapshots.</summary>
public interface IGuideDraftSnapshotRepository
{
    Task<GuideDraftSnapshot?> GetAsync(
        GuideDraftSnapshotId id, CancellationToken cancellationToken = default);

    /// <summary>A guide's snapshots, newest first.</summary>
    Task<IReadOnlyList<GuideDraftSnapshot>> ListForGuideAsync(
        GuideId guideId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes everything but the newest <paramref name="keep"/> snapshots. Called by the
    /// auto-save handler after every write, so the table's size is bounded by construction.
    /// </summary>
    Task PruneAsync(GuideId guideId, int keep, CancellationToken cancellationToken = default);

    Task AddAsync(GuideDraftSnapshot snapshot, CancellationToken cancellationToken = default);
}
