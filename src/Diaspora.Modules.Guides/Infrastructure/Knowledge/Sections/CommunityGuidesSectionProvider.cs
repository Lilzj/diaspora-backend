using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Extensibility.Knowledge;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge.Sections;

/// <summary>
/// What members wrote, ordered by what other members found useful.
/// <para>
/// <b>Most of the knowledge on this platform lives here, and the shelf exists to say so out
/// loud.</b> An official guide tells you the rule. A community guide tells you that the office
/// closes at eleven on Fridays, that the form they ask for is not the one on the website, and which
/// queue to join. Both are necessary and only one of them will ever be verified by an authority.
/// </para>
/// <para>
/// Newest rather than most-helpful, deliberately, and it is the one shelf ordered that way. The
/// most-helpful shelf above already surfaces the community guides that earned their place; this one
/// exists so a guide written last week by somebody who just went through it can be found before it
/// has accumulated any votes at all — which for a community shelf is the whole job.
/// </para>
/// </summary>
public sealed class CommunityGuidesSectionProvider : IKnowledgeSectionProvider
{
    private readonly KnowledgeGuideFinder _finder;

    public CommunityGuidesSectionProvider(KnowledgeGuideFinder finder) => _finder = finder;

    public string Key => KnowledgeWireNames.ToWire(KnowledgeSectionKind.Community);

    public int Order => 50;

    public async Task<KnowledgeSectionContent?> BuildAsync(
        KnowledgeSectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = _finder.Filter(
            request.CountryCode,
            request.GuideCategoryKeys,
            request.Language,
            GuideSortOrder.Newest,
            request.Limit + request.CuratedGuideIds.Count);

        var found = await _finder.FindIdsAsync(filter, cancellationToken);

        return new KnowledgeSectionContent
        {
            Key = Key,
            Title = "From the community",
            GuideIds =
            [
                .. found
                    .Where(guideId => !request.CuratedGuideIds.Contains(guideId))
                    .Take(request.Limit)
            ]
        };
    }
}
