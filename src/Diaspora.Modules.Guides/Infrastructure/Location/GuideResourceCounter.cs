using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Infrastructure.Location;

/// <summary>
/// Contributes the guide count to Location's neighbourhood hub and country page.
/// <para>
/// <c>guides</c> is one of the resource-count keys B4's contract freeze already published — the
/// country page's <c>resourceCounts</c> names <c>providers</c> and <c>guides</c> explicitly — so
/// B8 claims a key reserved for it rather than inventing one. This is the whole of what B8 adds
/// to Location: one class, in this module, and nothing at all in that one.
/// </para>
/// <para>
/// Counting is separate from listing because the costs differ by two orders of magnitude, which
/// is why B4 split the seams. Both counts are a single aggregate over an indexed column, and
/// both are published-only: a hub that counted drafts would advertise knowledge nobody can read.
/// </para>
/// </summary>
public sealed class GuideResourceCounter : ILocationResourceCounter
{
    /// <summary>The resource-count key B4's contract freeze reserved for guides.</summary>
    public const string Key = "guides";

    private readonly IGuideRepository _guides;

    public GuideResourceCounter(IGuideRepository guides) => _guides = guides;

    public string ResourceType => Key;

    public async Task<int> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Prefix match on the materialised path, so "London" counts everything beneath it
        // without a recursive descent. A guide scoped only to its country contributes to no
        // neighbourhood, which is correct: knowledge about the UK is not knowledge about Peckham.
        return await _guides.CountInAreaAsync(path.Value, cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        return await _guides.CountInCountryAsync(countryCode.Value, cancellationToken);
    }
}
