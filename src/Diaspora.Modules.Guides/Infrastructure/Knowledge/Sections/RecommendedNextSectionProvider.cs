using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Recommendations;
using Diaspora.Modules.Guides.Extensibility.Knowledge;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge.Sections;

/// <summary>
/// Where a curator says to go after the guide they put first.
/// <para>
/// <b>Curated sequencing, and the brief is explicit that it stays that way.</b> Nothing here infers
/// an ordering, learns from what readers did next, or scores a pair of guides for similarity. A
/// person who watched somebody get caught out wrote "read this before you sign anything", and that
/// sentence is knowledge of exactly the kind this platform exists to carry.
/// </para>
/// <para>
/// <b>It hangs off the curated shelf, which is what makes it possible on a category page at
/// all.</b> Recommendations are attached to a source guide, and a category page has no single guide
/// — except the one a curator put first, which is precisely the guide most readers will have just
/// finished. Without a curated shelf the section does not apply, and that is honest: nobody has said
/// what comes first, so nobody has said what comes after it.
/// </para>
/// <para>
/// When it arrives, an inferred version of this shelf replaces the repository behind it and nothing
/// above this class changes — which is the reason the curated version is worth building first.
/// </para>
/// </summary>
public sealed class RecommendedNextSectionProvider : IKnowledgeSectionProvider
{
    private readonly IKnowledgeRecommendationRepository _recommendations;

    public RecommendedNextSectionProvider(IKnowledgeRecommendationRepository recommendations) =>
        _recommendations = recommendations;

    public string Key => KnowledgeWireNames.ToWire(KnowledgeSectionKind.RecommendedNext);

    public int Order => 60;

    public bool AppliesTo(KnowledgeSectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.CuratedGuideIds.Count > 0;
    }

    public async Task<KnowledgeSectionContent?> BuildAsync(
        KnowledgeSectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CuratedGuideIds.Count == 0)
        {
            return null;
        }

        var source = GuideId.From(request.CuratedGuideIds[0]);

        var recommendation = await _recommendations.GetBySourceForReadAsync(
            source, cancellationToken);

        if (recommendation is null || recommendation.Reads.Count == 0)
        {
            return null;
        }

        return new KnowledgeSectionContent
        {
            Key = Key,
            Title = "Read next",
            GuideIds =
            [
                .. recommendation.Reads
                    .OrderBy(read => read.Position)
                    .Select(read => read.GuideId)

                    // Never point back at something already on the shelf above. A "read next" that
                    // recommends the guide the reader just opened is the shelf failing visibly.
                    .Where(guideId => !request.CuratedGuideIds.Contains(guideId))
                    .Take(request.Limit)
            ]
        };
    }
}
