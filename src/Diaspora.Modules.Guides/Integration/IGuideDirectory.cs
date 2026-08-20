using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Integration;

/// <summary>
/// The read surface other modules use to look guides up.
/// <para>
/// A Community Update that embeds a guide, a country page that lists its essential guides, and
/// every vertical after them stores a <see cref="GuideId"/> and asks here, rather than growing
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
/// </summary>
public interface IGuideDirectory
{
    /// <summary>One guide, or null when it does not exist or is not published.</summary>
    Task<GuideSummary?> FindAsync(GuideId guideId, CancellationToken cancellationToken = default);

    /// <summary>One guide by its deep-link handle.</summary>
    Task<GuideSummary?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Several guides at once. The N+1 guard for any screen rendering a list of things that
    /// each reference a guide. Absent, unpublished and hidden guides are simply missing from
    /// the answer.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, GuideSummary>> FindManyAsync(
        IReadOnlyCollection<GuideId> guideIds, CancellationToken cancellationToken = default);

    /// <summary>Whether the guide exists and is published. For validating a stored reference.</summary>
    Task<bool> ExistsAsync(GuideId guideId, CancellationToken cancellationToken = default);
}
