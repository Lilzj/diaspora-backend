using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Recommendations;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Serves the next-reads panel.
/// <para>
/// <b>An empty answer is a valid one and is not an error.</b> Most guides have no curated follow-ons
/// — curation is a person's time, and there are far more guides than curator-hours. The panel simply
/// does not render, which is much better than a panel of algorithmic guesses presented as somebody's
/// recommendation.
/// </para>
/// <para>
/// Recommendations pointing at a guide that no longer resolves are dropped rather than nulled: at
/// the end of a guide, a dead link is worse than a shorter list.
/// </para>
/// </summary>
public sealed class ListRecommendedGuidesQueryHandler
    : IQueryHandler<ListRecommendedGuidesQuery, IReadOnlyList<RecommendedReadResponse>>
{
    private readonly IKnowledgeRecommendationRepository _recommendations;
    private readonly KnowledgeCardResolver _cards;
    private readonly IKnowledgeCache _cache;

    public ListRecommendedGuidesQueryHandler(
        IKnowledgeRecommendationRepository recommendations,
        KnowledgeCardResolver cards,
        IKnowledgeCache cache)
    {
        _recommendations = recommendations;
        _cards = cards;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<RecommendedReadResponse>>> Handle(
        ListRecommendedGuidesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(
            query.Limit ?? KnowledgeLimits.MaxRecommendedReads,
            1,
            KnowledgeLimits.MaxRecommendedReads);

        var key = KnowledgeCacheKeys.RecommendedReads(query.GuideId, limit);

        var cached = await _cache.GetAsync<CachedReads>(key, cancellationToken);

        if (cached is not null)
        {
            return Result.Success(cached.Reads);
        }

        var recommendation = await _recommendations.GetBySourceForReadAsync(
            GuideId.From(query.GuideId), cancellationToken);

        if (recommendation is null || recommendation.Reads.Count == 0)
        {
            return Result.Success<IReadOnlyList<RecommendedReadResponse>>([]);
        }

        var ordered = recommendation.Reads
            .OrderBy(read => read.Position)
            .Take(limit)
            .ToList();

        var cards = await _cards.ResolveAsync(
            [.. ordered.Select(read => read.GuideId)], cancellationToken);

        var reads = new List<RecommendedReadResponse>(ordered.Count);
        var position = 0;

        foreach (var read in ordered)
        {
            if (!cards.TryGetValue(read.GuideId, out var card))
            {
                continue;
            }

            reads.Add(new RecommendedReadResponse
            {
                // Renumbered over what actually resolved, so a client rendering "1, 2, 3" never
                // shows "1, 3" because the second guide was withdrawn this morning.
                Position = position++,
                Reason = read.Reason,
                Guide = card
            });
        }

        await _cache.SetAsync(key, new CachedReads(reads), cancellationToken: cancellationToken);

        return Result.Success<IReadOnlyList<RecommendedReadResponse>>(reads);
    }

    private sealed record CachedReads(IReadOnlyList<RecommendedReadResponse> Reads);
}
