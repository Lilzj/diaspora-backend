using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Extensibility.Knowledge;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge.Sections;

/// <summary>
/// The shelf a curator filled by hand, for this country and this subject.
/// <para>
/// <b>The only shelf on the page that is a human decision, and it comes first for that reason.</b>
/// Every other section is an observation — most read, most helpful, most recently touched. This one
/// is somebody who knows the country saying "start here", and no amount of engagement data produces
/// that for a guide published last week.
/// </para>
/// <para>
/// It applies only when a curator has actually placed something. A "Featured" heading over the
/// algorithmic top three would be a lie about where the recommendation came from.
/// </para>
/// </summary>
public sealed class FeaturedGuidesSectionProvider : IKnowledgeSectionProvider
{
    public string Key => KnowledgeWireNames.ToWire(KnowledgeSectionKind.Featured);

    public int Order => 10;

    public bool AppliesTo(KnowledgeSectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.CuratedGuideIds.Count > 0;
    }

    /// <summary>
    /// Returns the curated identifiers verbatim — no query at all.
    /// <para>
    /// The curated order is the answer, and re-querying to "check" the guides are still visible
    /// would reorder them by whatever the query ordered by. The card resolver drops anything that no
    /// longer resolves, which is where that check belongs.
    /// </para>
    /// </summary>
    public Task<KnowledgeSectionContent?> BuildAsync(
        KnowledgeSectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult<KnowledgeSectionContent?>(new KnowledgeSectionContent
        {
            Key = Key,
            Title = "Start here",
            GuideIds = [.. request.CuratedGuideIds.Take(request.Limit)]
        });
    }
}
