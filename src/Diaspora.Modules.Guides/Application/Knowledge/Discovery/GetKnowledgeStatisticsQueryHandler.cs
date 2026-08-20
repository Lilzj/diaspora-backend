using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Serves one country's headline numbers.
/// <para>
/// <b>Answers for a country with no directory rather than failing.</b> A country the platform has
/// never arranged still has guides written about it — an author files a guide against a country, not
/// against a directory — and reporting zero guides because nobody has curated the page would be
/// wrong. The counts come from the guides; the directory contributes only the curated figures.
/// </para>
/// </summary>
public sealed class GetKnowledgeStatisticsQueryHandler
    : IQueryHandler<GetKnowledgeStatisticsQuery, KnowledgeStatisticsResponse>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly KnowledgeStatisticsBuilder _statistics;
    private readonly IKnowledgeCache _cache;

    public GetKnowledgeStatisticsQueryHandler(
        ICountryKnowledgeDirectoryRepository directories,
        KnowledgeStatisticsBuilder statistics,
        IKnowledgeCache cache)
    {
        _directories = directories;
        _statistics = statistics;
        _cache = cache;
    }

    public async Task<Result<KnowledgeStatisticsResponse>> Handle(
        GetKnowledgeStatisticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var country = KnowledgeNormalization.Country(query.CountryCode);

        if (country is null)
        {
            return Result.Failure<KnowledgeStatisticsResponse>(
                Domain.Errors.KnowledgeErrors.CountryInvalid);
        }

        var key = KnowledgeCacheKeys.Statistics(country);

        var cached = await _cache.GetAsync<KnowledgeStatisticsResponse>(key, cancellationToken);

        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var directory = await _directories.GetForReadAsync(country, cancellationToken);

        var statistics = await _statistics.BuildAsync(
            country,
            directory is { IsPublished: true } ? directory : null,
            cancellationToken);

        await _cache.SetAsync(key, statistics, cancellationToken: cancellationToken);

        return Result.Success(statistics);
    }
}
