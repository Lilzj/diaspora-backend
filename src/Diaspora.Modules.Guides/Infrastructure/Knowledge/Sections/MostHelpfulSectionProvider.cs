using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Extensibility.Knowledge;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge.Sections;

/// <summary>
/// Ranked by the helpful votes readers actually cast.
/// <para>
/// <b>Helpful votes rather than views, and the difference matters more here than anywhere else on
/// the platform.</b> A view means somebody clicked a title. A helpful vote means somebody read a
/// guide about renewing a work permit, went and renewed their work permit, and came back to say it
/// worked. For a knowledge base that is the only signal worth ranking on.
/// </para>
/// <para>
/// Curated guides are excluded, because they are already at the top of the page. A guide appearing
/// twice on one screen makes both shelves look broken.
/// </para>
/// </summary>
public sealed class MostHelpfulSectionProvider : IKnowledgeSectionProvider
{
    private readonly KnowledgeGuideFinder _finder;

    public MostHelpfulSectionProvider(KnowledgeGuideFinder finder) => _finder = finder;

    public string Key => KnowledgeWireNames.ToWire(KnowledgeSectionKind.MostHelpful);

    public int Order => 20;

    public async Task<KnowledgeSectionContent?> BuildAsync(
        KnowledgeSectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = _finder.Filter(
            request.CountryCode,
            request.GuideCategoryKeys,
            request.Language,
            GuideSortOrder.MostHelpful,

            // One page over, so excluding the curated guides afterwards still fills the shelf.
            request.Limit + request.CuratedGuideIds.Count);

        var found = await _finder.FindIdsAsync(filter, cancellationToken);

        return new KnowledgeSectionContent
        {
            Key = Key,
            Title = "Most helpful",
            GuideIds =
            [
                .. found
                    .Where(guideId => !request.CuratedGuideIds.Contains(guideId))
                    .Take(request.Limit)
            ]
        };
    }
}
