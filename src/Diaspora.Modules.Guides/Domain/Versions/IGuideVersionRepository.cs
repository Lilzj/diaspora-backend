using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Domain.Versions;

/// <summary>
/// Reads and writes version snapshots. Append-only in spirit — there is no removal, because
/// history outlives the guide it records.
/// </summary>
public interface IGuideVersionRepository
{
    Task<GuideVersion?> GetAsync(
        GuideVersionId id, CancellationToken cancellationToken = default);

    /// <summary>One version by its number. The restore and diff paths resolve through this.</summary>
    Task<GuideVersion?> GetByNumberAsync(
        GuideId guideId, int versionNumber, CancellationToken cancellationToken = default);

    /// <summary>A guide's versions, newest first, bounded by the listing ceiling.</summary>
    Task<IReadOnlyList<GuideVersion>> ListForGuideAsync(
        GuideId guideId, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(GuideVersion version, CancellationToken cancellationToken = default);
}
