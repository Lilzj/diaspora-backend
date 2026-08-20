using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// The single place the Knowledge Directory asks B8 for guides.
/// <para>
/// <b>Every shelf, every section, every dashboard band and every consumer of
/// <c>IKnowledgeDirectory</c> goes through here.</b> That is the point: the whole justification for
/// this phase is that six modules were about to write six versions of "find the guides for this
/// country and this subject", each with its own idea of whether an unlisted guide counts and its own
/// bug in the fallback. There is one version now, it lives here, and it composes a
/// <c>GuideQueryFilter</c> that B8 owns rather than a query of its own.
/// </para>
/// <para>
/// <b>Not one line of this class touches a <c>DbContext</c>.</b> The filter is a record of
/// primitives; the repository turns it into predicates it knows how to translate. That is what lets
/// a contributed section provider compose a shelf without being able to write a predicate EF fails
/// to translate inside somebody else's stack trace — the same discipline B8 applies to its ranking
/// strategies.
/// </para>
/// </summary>
public sealed class KnowledgeGuideFinder
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;

    public KnowledgeGuideFinder(IGuideRepository guides, GuideAuthorizationService authorization)
    {
        _guides = guides;
        _authorization = authorization;
    }

    /// <summary>
    /// Composes the filter every knowledge read shares.
    /// <para>
    /// Public so a section provider can build on it rather than assembling the audience fields by
    /// hand — a provider that forgot <see cref="GuideQueryFilter.ViewerIsStaff"/> would show a
    /// hidden guide on a public shelf, and it would look correct in every test written by the person
    /// who forgot.
    /// </para>
    /// </summary>
    public GuideQueryFilter Filter(
        string? countryCode,
        IReadOnlyCollection<string>? guideCategoryKeys = null,
        string? language = null,
        GuideSortOrder sort = GuideSortOrder.Newest,
        int limit = KnowledgeLimits.DefaultPageSize) =>
        new()
        {
            // Published only, always. The directory is a public surface, and a draft has an
            // audience of one — its author, on their own screen, which is B8's business.
            Statuses = [GuideStatus.Published],
            CountryCode = KnowledgeNormalization.Country(countryCode),
            CategoryKeys = Normalize(guideCategoryKeys),
            Language = string.IsNullOrWhiteSpace(language)
                ? null
                : language.Trim().ToLowerInvariant(),
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = sort,
            Limit = Clamp(limit)
        };

    /// <summary>One page of guides for a composed filter.</summary>
    public Task<CursorPagedResult<Guide>> PageAsync(
        GuideQueryFilter filter, string? cursor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return _guides.QueryAsync(filter, cursor, filter.EffectiveLimit, cancellationToken);
    }

    /// <summary>
    /// The identifiers a shelf holds, unpaged.
    /// <para>
    /// Identifiers rather than guides, because the caller is almost always a section provider whose
    /// results are about to be merged with six other shelves and resolved once. Returning full
    /// aggregates here would make the batching in <see cref="KnowledgeCardResolver"/> pointless.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Guid>> FindIdsAsync(
        GuideQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var page = await _guides.QueryAsync(
            filter, cursor: null, filter.EffectiveLimit, cancellationToken);

        return [.. page.Items.Select(guide => guide.Id.Value)];
    }

    /// <summary>
    /// How many published guides a country has for a set of filing categories.
    /// <para>
    /// Counted through the same filter the listing pages, so the badge on a directory row and the
    /// list behind it can never disagree about what they are counting.
    /// </para>
    /// </summary>
    public Task<int> CountAsync(
        string? countryCode,
        IReadOnlyCollection<string>? guideCategoryKeys,
        string? language = null,
        CancellationToken cancellationToken = default) =>
        CountAsync(Filter(countryCode, guideCategoryKeys, language), cancellationToken);

    /// <summary>How many published guides match a composed filter, exactly.</summary>
    public Task<int> CountAsync(
        GuideQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return _guides.CountAsync(filter, cancellationToken);
    }

    /// <summary>Every published guide in a country, counted through B8's own aggregate count.</summary>
    public Task<int> CountCountryAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        return country is null
            ? Task.FromResult(0)
            : _guides.CountInCountryAsync(country, cancellationToken);
    }

    private static int Clamp(int limit) =>
        Math.Clamp(limit, 1, KnowledgeLimits.MaxPageSize);

    private static IReadOnlyCollection<string> Normalize(IReadOnlyCollection<string>? keys) =>
        keys is null || keys.Count == 0
            ? []
            :
            [
                .. keys
                    .Select(KnowledgeNormalization.Key)
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
            ];
}
