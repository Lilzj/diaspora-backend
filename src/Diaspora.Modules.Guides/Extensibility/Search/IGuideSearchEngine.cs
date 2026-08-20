namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// The OpenSearch-independence port: how guide search is asked and how the index is fed.
/// <para>
/// <b>B8 exposes search projections; it does not implement search.</b> The shipped
/// <c>EfGuideSearchEngine</c> answers with a LIKE-style filter over the write model, which is
/// honest and adequate until a real engine arrives. A search module (B15) replaces the whole
/// implementation by registering its own class — the port is engine-independent, the documents
/// it ingests are flat, and nothing above this interface changes.
/// </para>
/// <para>
/// Index maintenance is best-effort by contract: <see cref="IndexAsync"/> and
/// <see cref="RemoveAsync"/> are called after the guide write has committed, and a failure
/// degrades to a stale index entry, never to a rolled-back publication.
/// </para>
/// </summary>
public interface IGuideSearchEngine
{
    /// <summary>Upserts one guide's document. Idempotent on the guide identifier.</summary>
    Task IndexAsync(GuideSearchDocument document, CancellationToken cancellationToken = default);

    /// <summary>Removes a guide from the index — deletion, archival, hiding, unpublishing.</summary>
    Task RemoveAsync(Guid guideId, CancellationToken cancellationToken = default);

    /// <summary>Answers a search with identifiers, never with documents — the read path
    /// hydrates cards from the write model, so a stale index can misrank but not misrender.</summary>
    Task<GuideSearchOutcome> SearchAsync(
        GuideSearchRequest request, CancellationToken cancellationToken = default);
}
