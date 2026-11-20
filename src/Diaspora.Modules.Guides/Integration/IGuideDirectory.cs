namespace Diaspora.Modules.Guides.Integration;

/// <summary>
/// The read surface other modules use to look guides up.
/// <para>
/// A Community Update that embeds a guide, a country page that lists its essential guides, and
/// every vertical after them stores a guide's identifier and asks here, rather than growing
/// a guide table of its own — the same promise B7's directory makes for providers, kept for
/// knowledge.
/// </para>
/// <para>
/// Deliberately narrower than <c>IGuideRepository</c>. A consumer needs to resolve a guide and
/// render a card; it does not need to publish one, edit one or read its drafts, and handing it
/// the repository would give it all three. Only published, unhidden guides resolve — a
/// consumer holding the id of a draft gets null, exactly as it would for a guide that never
/// existed.
/// </para>
/// <para>
/// <b>Identifiers cross this seam as <see cref="Guid"/>, not as B8's <c>GuideId</c>.</b> A
/// published seam that names a module-internal type obliges every consumer to import that
/// module's internals in order to call it, which is the opposite of what publishing a seam is
/// for. All four verticals that resolve guides already held a <see cref="Guid"/>, converted it
/// up to satisfy this signature and converted the answer back down again — <c>FindManyAsync</c>
/// has always keyed its result by <see cref="Guid"/>, so the seam already spoke
/// <see cref="Guid"/> in one direction. The conversion belongs on B8's side, where the
/// identifier is owned. BARCH-001 / BSOLID-003.
/// </para>
/// </summary>
public interface IGuideDirectory
{
    /// <summary>One guide, or null when it does not exist or is not published.</summary>
    Task<GuideSummary?> FindAsync(Guid guideId, CancellationToken cancellationToken = default);

    /// <summary>One guide by its deep-link handle.</summary>
    Task<GuideSummary?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Several guides at once. The N+1 guard for any screen rendering a list of things that
    /// each reference a guide. Absent, unpublished and hidden guides are simply missing from
    /// the answer.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, GuideSummary>> FindManyAsync(
        IReadOnlyCollection<Guid> guideIds, CancellationToken cancellationToken = default);

    /// <summary>Whether the guide exists and is published. For validating a stored reference.</summary>
    Task<bool> ExistsAsync(Guid guideId, CancellationToken cancellationToken = default);
}
