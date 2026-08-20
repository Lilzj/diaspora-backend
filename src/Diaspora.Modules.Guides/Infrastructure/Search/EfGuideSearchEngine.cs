using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Extensibility.Search;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Infrastructure.Search;

/// <summary>
/// The shipped search engine: the write model answers its own searches.
/// <para>
/// <b>The index and the table are the same rows, and that is the whole design.</b> A deployment
/// with a few thousand guides does not need OpenSearch, and running one so that a LIKE query can
/// be spelled differently is infrastructure nobody asked for. This engine delegates to the
/// repository's text filter, so a search is a query against indexed columns and there is exactly
/// one copy of the truth.
/// </para>
/// <para>
/// <b><see cref="IndexAsync"/> and <see cref="RemoveAsync"/> are therefore deliberate no-ops</b>,
/// not unimplemented stubs. There is nothing to feed: the guide row was written by the command
/// that raised the document, and a second store would only be a thing that could disagree with
/// the first. Callers stay written against the port, so the day a real engine registers itself
/// they begin feeding it without changing a line.
/// </para>
/// <para>
/// <b>What is honestly given up:</b> relevance. Filter order is not ranking, so hits carry no
/// score — a fabricated number would be worse than none — and results come back in the requested
/// sort order. Stemming, synonyms, typo tolerance and cross-language matching are what a real
/// engine buys, and <see cref="IGuideSearchEngine"/> exists so that buying it replaces this class
/// wholesale rather than editing anything above it.
/// </para>
/// <para>
/// Visibility is the repository's, not this class's: the filter carries no viewer, so only
/// published, non-hidden guides can match. An engine that over-returned would cost a wasted row
/// rather than leak a draft, but there is no reason to make it do either.
/// </para>
/// </summary>
public sealed class EfGuideSearchEngine : IGuideSearchEngine
{
    private readonly IGuideRepository _guides;

    public EfGuideSearchEngine(IGuideRepository guides) => _guides = guides;

    /// <summary>
    /// A no-op. The write model is the index — see the class summary.
    /// </summary>
    public Task IndexAsync(
        GuideSearchDocument document, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// A no-op. Removing a guide from this index means deleting the guide, which the command
    /// that called here has already done.
    /// </summary>
    public Task RemoveAsync(Guid guideId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task<GuideSearchOutcome> SearchAsync(
        GuideSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            Text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim(),
            CountryCode = request.CountryCode,
            CategoryKey = request.CategoryKey,
            Tag = request.Tag,
            Language = request.Language,
            AuthorId = request.AuthorId is { } authorId ? UserId.From(authorId) : null,
            VerifiedOnly = request.VerifiedOnly,
            FeaturedOnly = request.FeaturedOnly,
            Sort = request.Sort,
            Limit = request.Limit
        };

        var page = await _guides.QueryAsync(
            filter, request.Cursor, filter.EffectiveLimit, cancellationToken);

        return new GuideSearchOutcome
        {
            // No scores. Filter order is not relevance, and the caller ranks by the sort it asked
            // for rather than by a number this engine cannot compute.
            Hits = [.. page.Items.Select(guide => new GuideSearchHit { GuideId = guide.Id.Value })],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,

            // Null rather than a guess: the cursor query counts nothing, and a total the client
            // renders must be right or absent.
            Total = null
        };
    }
}
