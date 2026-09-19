using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Media.Domain.Assets;

/// <summary>The media asset's persistence boundary.</summary>
public interface IMediaAssetRepository
{
    /// <summary>
    /// One asset with its variants and grants.
    /// <para>
    /// Both are loaded eagerly because both are needed on the same paths: a read decides
    /// access from the grants and then serves from a variant, and a second round trip between
    /// those two steps buys nothing.
    /// </para>
    /// </summary>
    Task<MediaAsset?> GetAsync(MediaId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// One asset without its variants or grants — for a write path that touches neither.
    /// </summary>
    Task<MediaAsset?> GetSummaryAsync(MediaId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaAsset>> GetManyAsync(
        IReadOnlyCollection<MediaId> ids, CancellationToken cancellationToken = default);

    /// <summary>Every asset attached to one owning entity, in role and position order.</summary>
    Task<IReadOnlyList<MediaAsset>> GetByOwnerAsync(
        MediaOwner owner, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assets uploaded by a member, newest first.
    /// <para>
    /// Paged rather than complete: a member who has run a marketplace shop for two years has
    /// thousands, and a media library screen renders twenty.
    /// </para>
    /// </summary>
    Task<PagedResult<MediaAsset>> ListForUserAsync(
        UserId userId,
        MediaKind? kind,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assets whose bytes have the given digest.
    /// <para>
    /// Serves deduplication on the write path and reuse detection on the read path. The client
    /// fingerprints re-used images by URL, which any re-upload defeats; this is the same
    /// question asked of the bytes. Media answers it and forms no opinion — what constitutes
    /// abusive reuse is Trust's judgement.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<MediaAsset>> FindByHashAsync(
        ContentHash hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assets nothing ever claimed, older than the grace period.
    /// <para>
    /// A vertical that uploads a photo and then fails to save its listing leaves one of these.
    /// Without a sweep they accumulate silently and forever, which is the failure mode every
    /// hand-rolled upload endpoint eventually discovers.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<MediaAsset>> FindOrphansAsync(
        DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default);

    /// <summary>Assets whose bytes are gone and whose rows may now go too.</summary>
    Task<IReadOnlyList<MediaAsset>> FindDeletedAsync(
        DateTimeOffset deletedBefore, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assets a scanner refused, oldest first.
    /// <para>
    /// The queue behind the release route. Oldest first because a quarantined file is a member's
    /// upload that silently did not appear, and the longest wait is the one nobody has looked at.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<MediaAsset>> FindQuarantinedAsync(
        int limit, CancellationToken cancellationToken = default);

    Task AddAsync(MediaAsset asset, CancellationToken cancellationToken = default);

    void Remove(MediaAsset asset);
}
