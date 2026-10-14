using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// Turns a list of guide identifiers into the cards a shelf renders, in the order asked for.
/// <para>
/// <b>The N+1 guard the whole composed-read design rests on.</b> A country page has a featured
/// shelf, six section shelves and a learning path, and every one of them produces identifiers.
/// Resolving them shelf by shelf would be seven queries with overlapping results; resolving them
/// one at a time would be forty. This collects the whole page's identifiers, asks the guide
/// repository once, and hands each shelf the cards it wanted.
/// </para>
/// <para>
/// <b>Order is restored from the request, not taken from the database.</b> A curated shelf is
/// curated — the third guide is third because somebody put it there — and a repository that ordered
/// by publication date would silently discard the curation while returning exactly the right set.
/// </para>
/// <para>
/// <b>Guides that no longer resolve are dropped rather than nulled.</b> A guide unpublished since a
/// curator placed it should vanish from the reader's shelf; the curator's own view is the one that
/// still shows the row, because that is the person who needs to know.
/// </para>
/// </summary>
public sealed class KnowledgeCardResolver
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideAuthorNames _authorNames;

    public KnowledgeCardResolver(
        IGuideRepository guides,
        GuideAuthorizationService authorization,
        GuideAuthorNames authorNames)
    {
        _guides = guides;
        _authorization = authorization;
        _authorNames = authorNames;
    }

    /// <summary>
    /// Resolves identifiers to cards, keyed by identifier.
    /// <para>
    /// A dictionary rather than a list, because the caller has several shelves to fill from one
    /// resolution and each wants its own subset in its own order.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, GuideCardResponse>> ResolveAsync(
        IReadOnlyCollection<Guid> guideIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideIds);

        var distinct = guideIds.Where(id => id != Guid.Empty).Distinct().ToList();

        if (distinct.Count == 0)
        {
            return new Dictionary<Guid, GuideCardResponse>();
        }

        var guides = new List<Guide>(distinct.Count);

        // The repository clamps a page to MaxPageSize, so a set larger than one page is resolved in
        // chunks. Chunking here rather than raising the clamp keeps one ceiling for every caller —
        // a repository whose limit depended on who was asking would be a repository nobody could
        // reason about.
        foreach (var chunk in distinct.Chunk(GuideLimits.MaxPageSize))
        {
            var page = await _guides.QueryAsync(
                new GuideQueryFilter
                {
                    Statuses = [GuideStatus.Published],
                    GuideIds = chunk,
                    ViewerId = _authorization.CallerId,
                    ViewerIsStaff = _authorization.IsStaff,
                    Limit = chunk.Length
                },
                cursor: null,
                limit: chunk.Length,
                cancellationToken);

            guides.AddRange(page.Items);
        }

        // Every author across every chunk in one lookup.
        var cards = await _authorNames.ToCardsAsync(guides, cancellationToken);

        return cards.ToDictionary(card => card.GuideId);
    }

    /// <summary>
    /// Picks the cards for one shelf out of a resolved set, preserving the shelf's order and
    /// dropping whatever no longer resolves.
    /// </summary>
    public static IReadOnlyList<GuideCardResponse> Order(
        IReadOnlyCollection<Guid> guideIds,
        IReadOnlyDictionary<Guid, GuideCardResponse> resolved)
    {
        ArgumentNullException.ThrowIfNull(guideIds);
        ArgumentNullException.ThrowIfNull(resolved);

        var cards = new List<GuideCardResponse>(guideIds.Count);

        foreach (var guideId in guideIds)
        {
            if (resolved.TryGetValue(guideId, out var card))
            {
                cards.Add(card);
            }
        }

        return cards;
    }
}
