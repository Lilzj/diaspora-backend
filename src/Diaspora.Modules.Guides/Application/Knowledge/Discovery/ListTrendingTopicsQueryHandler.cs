using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Serves the trending list, and — for a curator — the commissioning queue.
/// <para>
/// <b>The unanswered list is curator-only, deliberately.</b> The set of questions this platform
/// cannot answer is precisely the set a competitor would find most useful, and it is of no use at
/// all to a reader. A reader asking for it gets the ordinary trending list rather than an error.
/// </para>
/// </summary>
public sealed class ListTrendingTopicsQueryHandler
    : IQueryHandler<ListTrendingTopicsQuery, IReadOnlyList<TrendingTopicResponse>>
{
    private readonly IKnowledgeSearchTermRepository _terms;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IOptions<KnowledgeOptions> _options;
    private readonly IClock _clock;

    public ListTrendingTopicsQueryHandler(
        IKnowledgeSearchTermRepository terms,
        KnowledgeAuthorizationService authorization,
        IOptions<KnowledgeOptions> options,
        IClock clock)
    {
        _terms = terms;
        _authorization = authorization;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<TrendingTopicResponse>>> Handle(
        ListTrendingTopicsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var country = KnowledgeNormalization.Country(query.CountryCode) ?? string.Empty;

        var limit = Math.Clamp(
            query.Limit ?? KnowledgeLimits.MaxTrendingTopics, 1, KnowledgeLimits.MaxTrendingTopics);

        var terms = query.UnansweredOnly && _authorization.IsCurator
            ? await _terms.UnansweredAsync(country, limit, cancellationToken)
            : await _terms.TopAsync(country, limit, Window(), cancellationToken);

        return Result.Success<IReadOnlyList<TrendingTopicResponse>>(
        [
            .. terms.Select(term => new TrendingTopicResponse
            {
                Term = term.Term,
                SearchCount = term.SearchCount,
                ResultCount = term.ResultCount,
                IsUnanswered = term.IsUnanswered,
                LastSearchedAt = term.LastSearchedAt
            })
        ]);
    }

    /// <summary>
    /// How far back trending looks. Editorial rather than technical: a launch community wants a long
    /// window so anything at all trends, and a busy one wants a short window so trending means this
    /// week rather than this quarter.
    /// </summary>
    private DateTimeOffset Window() =>
        _clock.UtcNow.AddDays(-Math.Max(1, _options.Value.TrendingWindowDays));
}
