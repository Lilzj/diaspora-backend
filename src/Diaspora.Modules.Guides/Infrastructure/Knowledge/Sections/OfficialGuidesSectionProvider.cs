using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Extensibility.Knowledge;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge.Sections;

/// <summary>
/// Guides carrying an authority badge from Trust.
/// <para>
/// <b>Narrower than verified, and the distinction is the point of having the shelf at all.</b> A
/// verified guide has been checked by somebody. An official one came from the body that makes the
/// rule — an embassy, a tax authority, a registrar — and Trust records that as the badge on the
/// mark. Somebody deciding whether to act on what they are reading needs that distinction, and it is
/// most of why they are reading it.
/// </para>
/// <para>
/// <b>B8.1 does not decide what is official.</b> Trust owns the badge and this reads it, exactly as
/// ADR-002 requires: Location owns the claim, Trust owns the proof, and the knowledge layer renders
/// the answer without an opinion of its own.
/// </para>
/// </summary>
public sealed class OfficialGuidesSectionProvider : IKnowledgeSectionProvider
{
    private readonly KnowledgeGuideFinder _finder;

    public OfficialGuidesSectionProvider(KnowledgeGuideFinder finder) => _finder = finder;

    public string Key => KnowledgeWireNames.ToWire(KnowledgeSectionKind.Official);

    public int Order => 40;

    public async Task<KnowledgeSectionContent?> BuildAsync(
        KnowledgeSectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = _finder.Filter(
            request.CountryCode,
            request.GuideCategoryKeys,
            request.Language,
            GuideSortOrder.MostHelpful,
            request.Limit) with
        {
            OfficialOnly = true
        };

        return new KnowledgeSectionContent
        {
            Key = Key,
            Title = "Official guidance",
            GuideIds = await _finder.FindIdsAsync(filter, cancellationToken)
        };
    }
}
