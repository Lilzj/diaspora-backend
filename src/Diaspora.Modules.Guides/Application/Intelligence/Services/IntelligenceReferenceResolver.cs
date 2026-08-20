using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;

namespace Diaspora.Modules.Guides.Application.Intelligence.Services;

/// <summary>
/// Resolves the categories and authorities a page of notices needs, once.
/// <para>
/// <b>The N+1 guard the whole read layer rests on.</b> Every notice card carries its category's
/// localized label and its authority's name, and both live in other aggregates. Resolving them per
/// card turns a twenty-row page into forty-one round trips, and a dashboard — which composes five
/// bands over the same handful of categories and authorities — into rather more. This collects the
/// identifiers a page actually uses, asks twice, and hands the results back as dictionaries the
/// mapper indexes into.
/// </para>
/// <para>
/// <b>Archived authorities resolve too.</b> A notice published under a ministry that has since been
/// restructured still has to render who issued it — a live notice with a blank source is worse than
/// one naming a body that no longer exists under that name. The repository's batch read ignores the
/// soft-delete filter for exactly this.
/// </para>
/// <para>
/// Scoped, and it caches nothing between requests. A per-request dictionary is the whole win; a
/// longer-lived cache would need invalidating when a category is renamed, which is a problem this
/// does not have to have.
/// </para>
/// </summary>
public sealed class IntelligenceReferenceResolver
{
    private readonly INoticeCategoryRepository _categories;
    private readonly IIssuingAuthorityRepository _authorities;

    public IntelligenceReferenceResolver(
        INoticeCategoryRepository categories, IIssuingAuthorityRepository authorities)
    {
        _categories = categories;
        _authorities = authorities;
    }

    /// <summary>Everything a page of notices needs to render, in two round trips.</summary>
    public async Task<IntelligenceReferences> ResolveAsync(
        IReadOnlyCollection<OfficialNotice> notices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notices);

        if (notices.Count == 0)
        {
            return IntelligenceReferences.Empty;
        }

        var categoryKeys = notices
            .Select(notice => notice.CategoryKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var authorityIds = notices
            .Select(notice => notice.AuthorityId)
            .Distinct()
            .ToList();

        var categories = await _categories.GetByKeysAsync(categoryKeys, cancellationToken);
        var authorities = await _authorities.GetByIdsAsync(authorityIds, cancellationToken);

        return new IntelligenceReferences(categories, authorities);
    }

    /// <summary>The authorities a set of identifiers names, for a surface with no notices in it.</summary>
    public async Task<IReadOnlyDictionary<IssuingAuthorityId, IssuingAuthority>> ResolveAuthoritiesAsync(
        IReadOnlyCollection<IssuingAuthorityId> ids,
        CancellationToken cancellationToken = default) =>
        await _authorities.GetByIdsAsync(ids, cancellationToken);
}

/// <summary>
/// What a page of notices needed, resolved.
/// <para>
/// A lookup that returns null on a miss rather than throwing: a notice filed under a category
/// somebody archived and then hard-removed from the seed is a data fault, and failing the whole page
/// over one card is the wrong trade on a screen somebody may be reading during an emergency.
/// </para>
/// </summary>
public sealed class IntelligenceReferences
{
    internal IntelligenceReferences(
        IReadOnlyDictionary<string, NoticeCategory> categories,
        IReadOnlyDictionary<IssuingAuthorityId, IssuingAuthority> authorities)
    {
        Categories = categories;
        Authorities = authorities;
    }

    /// <summary>An empty set, for a page with no rows on it.</summary>
    public static IntelligenceReferences Empty { get; } = new(
        new Dictionary<string, NoticeCategory>(StringComparer.Ordinal),
        new Dictionary<IssuingAuthorityId, IssuingAuthority>());

    public IReadOnlyDictionary<string, NoticeCategory> Categories { get; }

    public IReadOnlyDictionary<IssuingAuthorityId, IssuingAuthority> Authorities { get; }

    public NoticeCategory? Category(string key) =>
        Categories.TryGetValue(key, out var category) ? category : null;

    public IssuingAuthority? Authority(IssuingAuthorityId id) =>
        Authorities.TryGetValue(id, out var authority) ? authority : null;
}
