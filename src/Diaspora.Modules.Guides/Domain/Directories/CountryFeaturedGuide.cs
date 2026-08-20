using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Directories;

/// <summary>
/// One guide a curator put in front of everybody arriving in one country.
/// <para>
/// <b>Country-scoped featuring, which the global flag on a guide cannot express.</b> B8 already
/// carries an <c>IsFeatured</c> boolean, and it answers a platform-wide question. This answers a
/// per-country one, and the same guide is legitimately the first thing to read about Mauritius and
/// completely irrelevant to somebody moving to Canada. A single flag has to be wrong for one of
/// them.
/// </para>
/// <para>
/// <b>It holds an identifier and a note, and nothing else.</b> No title, no summary, no author — a
/// featured row that cached the guide's title would show the old one after a retitle, and the
/// directory does not own guide content. The read path resolves identifiers through B8's own
/// directory contract, in one batched call.
/// </para>
/// </summary>
public sealed class CountryFeaturedGuide
{
    private CountryFeaturedGuide(
        Guid featureId,
        Guid guideId,
        string? knowledgeCategoryKey,
        int position,
        string? note,
        DateTimeOffset featuredAt)
    {
        FeatureId = featureId;
        GuideId = guideId;
        KnowledgeCategoryKey = knowledgeCategoryKey;
        Position = position;
        Note = note;
        FeaturedAt = featuredAt;
    }

    private CountryFeaturedGuide() { }

    /// <summary>
    /// The row's own identity.
    /// <para>
    /// A surrogate rather than the natural (guide, category) pair, because the category half is
    /// nullable — a guide can be featured on the country's own shelf rather than within a category
    /// — and a nullable column inside a composite primary key is a shape no provider handles the
    /// same way. Uniqueness of the pair is enforced by the aggregate, which is where the rule
    /// actually lives.
    /// </para>
    /// </summary>
    public Guid FeatureId { get; private set; }

    /// <summary>A B8 guide identifier, stored as a primitive. Never resolved inside the aggregate.</summary>
    public Guid GuideId { get; private set; }

    /// <summary>The category it was featured within, or null for the country's own shelf.</summary>
    public string? KnowledgeCategoryKey { get; private set; }

    /// <summary>Position within its scope. Contiguous from zero, renumbered on every change.</summary>
    public int Position { get; private set; }

    /// <summary>The curator's one-line reason. Rendered verbatim, never interpreted.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset FeaturedAt { get; private set; }

    internal static CountryFeaturedGuide? TryCreate(
        Guid featureId,
        Guid guideId,
        string? knowledgeCategoryKey,
        int position,
        string? note,
        DateTimeOffset featuredAt)
    {
        if (guideId == Guid.Empty)
        {
            return null;
        }

        // A supplied category key must be a usable one; an absent key means the country shelf and
        // is not an error. Distinguishing the two is the whole reason this returns null only for
        // the first case.
        string? scope = null;

        if (!string.IsNullOrWhiteSpace(knowledgeCategoryKey))
        {
            scope = KnowledgeNormalization.Key(knowledgeCategoryKey);

            if (scope is null)
            {
                return null;
            }
        }

        return new CountryFeaturedGuide(
            featureId,
            guideId,
            scope,
            position,
            KnowledgeNormalization.Text(note, KnowledgeLimits.MaxCuratorNote),
            featuredAt);
    }

    internal void MoveTo(int position) => Position = position;

    /// <summary>Whether this row occupies the same shelf slot as the guide and scope given.</summary>
    internal bool Matches(Guid guideId, string? scope) =>
        GuideId == guideId
        && string.Equals(KnowledgeCategoryKey, scope, StringComparison.Ordinal);
}
