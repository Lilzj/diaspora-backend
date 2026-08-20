using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Extensibility.Knowledge;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge.Sections;

/// <summary>
/// Ranked by last edit — the shelf that answers "is any of this still true".
/// <para>
/// <b>Freshness is a correctness signal in this domain, not a novelty one.</b> A social feed sorts
/// by recency because new things are interesting. A knowledge base sorts by recency because visa
/// fees change, deadlines move and a guide last touched three years ago may be actively harmful.
/// This is the shelf somebody checks before acting on what they read.
/// </para>
/// </summary>
public sealed class RecentlyUpdatedSectionProvider : IKnowledgeSectionProvider
{
    private readonly KnowledgeGuideFinder _finder;

    public RecentlyUpdatedSectionProvider(KnowledgeGuideFinder finder) => _finder = finder;

    public string Key => KnowledgeWireNames.ToWire(KnowledgeSectionKind.RecentlyUpdated);

    public int Order => 30;

    public async Task<KnowledgeSectionContent?> BuildAsync(
        KnowledgeSectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = _finder.Filter(
            request.CountryCode,
            request.GuideCategoryKeys,
            request.Language,
            GuideSortOrder.RecentlyUpdated,
            request.Limit);

        return new KnowledgeSectionContent
        {
            Key = Key,
            Title = "Recently updated",
            GuideIds = await _finder.FindIdsAsync(filter, cancellationToken)
        };
    }
}
