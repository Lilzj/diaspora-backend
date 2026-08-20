using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Extensibility.Search;

namespace Diaspora.Modules.Guides.Infrastructure.Search;

/// <summary>
/// The shipped knowledge search engine: the write model answers its own searches.
/// <para>
/// <b>The index and the table are the same rows.</b> A deployment with a few thousand guides does
/// not need OpenSearch, and running one so a LIKE query can be spelled differently is infrastructure
/// nobody asked for. This engine composes a <c>GuideQueryFilter</c> and hands it to B8's repository,
/// so a knowledge search is a query against indexed columns and there is exactly one copy of the
/// truth.
/// </para>
/// <para>
/// <b>What is honestly given up is relevance.</b> Filter order is not ranking, so hits carry no
/// score — a fabricated number would be worse than none — and results come back in the requested
/// sort order. A request for <c>relevance</c> is answered as most-helpful, which is the closest
/// honest approximation this engine can make and is a far better default for a knowledge base than
/// recency: somebody searching "renew work permit" wants the guide that worked for other people, not
/// the one written most recently.
/// </para>
/// <para>
/// <b>Stemming, synonyms, typo tolerance and cross-language matching are what a real engine
/// buys</b>, and <see cref="IKnowledgeSearchEngine"/> exists so buying it replaces this class
/// wholesale rather than editing anything above it. The request carries only primitives and
/// enumerable filters precisely so an adapter can translate it into a query DSL.
/// </para>
/// </summary>
public sealed class EfKnowledgeSearchEngine : IKnowledgeSearchEngine
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;

    public EfKnowledgeSearchEngine(
        IGuideRepository guides, GuideAuthorizationService authorization)
    {
        _guides = guides;
        _authorization = authorization;
    }

    public string Name => "ef-write-model";

    /// <summary>
    /// Always available. The database that answers every other query answers this one, so there is
    /// no separate dependency that can be down.
    /// </summary>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public async Task<KnowledgeSearchOutcome> SearchAsync(
        KnowledgeSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            Text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim(),
            CountryCode = request.CountryCode,
            CategoryKeys = request.GuideCategoryKeys,
            GuideIds = request.GuideIds,
            Tag = request.Tag,
            Language = request.Language,
            VerifiedOnly = request.VerifiedOnly,
            OfficialOnly = request.OfficialOnly,
            FeaturedOnly = request.FeaturedOnly,
            Difficulty = KnowledgeWireNames.ParseDifficulty(request.Difficulty),
            MaxReadingTimeMinutes = request.MaxReadingTimeMinutes,

            // Visibility is computed here and applied by the repository. An engine that composed its
            // own would be a second implementation of who may see what.
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = _authorization.IsStaff,

            Sort = SortOf(request.Sort),
            Limit = request.Limit
        };

        var page = await _guides.QueryAsync(
            filter, request.Cursor, filter.EffectiveLimit, cancellationToken);

        return new KnowledgeSearchOutcome
        {
            // No scores. Filter order is not relevance, and the caller ranks by the sort it asked
            // for rather than by a number this engine cannot compute.
            Hits = [.. page.Items.Select(guide => new KnowledgeSearchHit { GuideId = guide.Id.Value })],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,

            // Null rather than a guess: the cursor query counts nothing, and a total a client
            // renders must be right or absent.
            TotalCount = null,
            Engine = Name
        };
    }

    /// <summary>
    /// Maps the knowledge sort vocabulary onto B8's, which knows how to order and page by it.
    /// <para>
    /// Relevance becomes most-helpful. That is the honest substitution: this engine cannot score,
    /// and of the orderings it can produce, "what other people found useful" is the one closest to
    /// what somebody typing a question actually wants.
    /// </para>
    /// </summary>
    private static GuideSortOrder SortOf(string? sort) =>
        KnowledgeWireNames.IsRelevance(sort) || string.IsNullOrWhiteSpace(sort)
            ? GuideSortOrder.MostHelpful
            : GuideWireNames.ParseSort(sort);
}
