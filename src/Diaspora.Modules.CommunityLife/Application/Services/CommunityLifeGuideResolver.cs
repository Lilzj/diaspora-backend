using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Integration;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// The one class in B12 that reads B8's guide directory.
/// <para>
/// Resolves the identifiers a client supplies into references carrying a cached title and slug, so a
/// card renders a festival guide beside an event without a call into Guides. Unknown or unpublished
/// guides are dropped rather than refused: a guide that was unpublished should stop being offered,
/// and refusing the whole write over one stale reference would be disproportionate.
/// </para>
/// <para>
/// <b>Optional by design.</b> A deployment that does not load Guides has no directory, so the
/// contract is injected nullable and the module simply attaches nothing.
/// </para>
/// </summary>
public sealed class CommunityLifeGuideResolver
{
    private readonly IGuideDirectory? _guides;

    public CommunityLifeGuideResolver(IGuideDirectory? guides = null) => _guides = guides;

    /// <summary>Guide references for an event.</summary>
    public async Task<IReadOnlyList<EventGuideReference>> ForEventAsync(
        IReadOnlyList<Guid>? guideIds, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(guideIds, cancellationToken);

        return
        [
            .. resolved.Select(guide =>
                EventGuideReference.Create(guide.Key, guide.Value.Title, guide.Value.Slug))
        ];
    }

    /// <summary>Guide references for an organization.</summary>
    public async Task<IReadOnlyList<OrganizationGuideReference>> ForOrganizationAsync(
        IReadOnlyList<Guid>? guideIds, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(guideIds, cancellationToken);

        return
        [
            .. resolved.Select(guide =>
                OrganizationGuideReference.Create(guide.Key, guide.Value.Title, guide.Value.Slug))
        ];
    }

    private async Task<IReadOnlyDictionary<Guid, GuideSummary>> ResolveAsync(
        IReadOnlyList<Guid>? guideIds, CancellationToken cancellationToken)
    {
        if (_guides is null || guideIds is not { Count: > 0 })
        {
            return new Dictionary<Guid, GuideSummary>();
        }

        var wanted = guideIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Select(GuideId.From)
            .ToList();

        return wanted.Count == 0
            ? new Dictionary<Guid, GuideSummary>()
            : await _guides.FindManyAsync(wanted, cancellationToken);
    }
}
