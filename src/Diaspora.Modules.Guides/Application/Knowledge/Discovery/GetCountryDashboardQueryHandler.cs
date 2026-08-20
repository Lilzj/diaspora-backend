using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Serves a country dashboard.
/// <para>
/// <b>Cached harder than anything else in the phase, and it earns it.</b> A dashboard is seven
/// queried shelves, a curated one, a breakdown with a count per row, a trending list and a set of
/// contributed context bands. Nothing on it changes when one guide is published, and everything on
/// it changes when a curator arranges the country — which is exactly the invalidation the cache
/// already does.
/// </para>
/// <para>
/// No view is recorded here. Opening a dashboard and opening a directory are the same visit from a
/// reader's point of view, and counting both would double every country's interest figure depending
/// on which screen the client happened to render first.
/// </para>
/// </summary>
public sealed class GetCountryDashboardQueryHandler
    : IQueryHandler<GetCountryDashboardQuery, CountryDashboardResponse>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly CountryDashboardComposer _composer;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;

    public GetCountryDashboardQueryHandler(
        ICountryKnowledgeDirectoryRepository directories,
        CountryDashboardComposer composer,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache)
    {
        _directories = directories;
        _composer = composer;
        _authorization = authorization;
        _cache = cache;
    }

    public async Task<Result<CountryDashboardResponse>> Handle(
        GetCountryDashboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var directory = await _directories.GetForReadAsync(
            query.CountryCode, cancellationToken);

        if (directory is null)
        {
            return Result.Failure<CountryDashboardResponse>(KnowledgeErrors.DirectoryNotFound);
        }

        if (!directory.IsPublished && !_authorization.IsCurator)
        {
            return Result.Failure<CountryDashboardResponse>(KnowledgeErrors.DirectoryNotFound);
        }

        var cacheable = directory.IsPublished
            && !_authorization.IsCurator
            && string.IsNullOrWhiteSpace(query.Language);

        if (!cacheable)
        {
            return Result.Success(
                await _composer.ComposeAsync(
                    directory, query.Locale, query.Language, cancellationToken));
        }

        var key = KnowledgeCacheKeys.Dashboard(directory.CountryCode, query.Locale);

        var cached = await _cache.GetAsync<CountryDashboardResponse>(key, cancellationToken);

        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var composed = await _composer.ComposeAsync(
            directory, query.Locale, query.Language, cancellationToken);

        await _cache.SetAsync(key, composed, cancellationToken: cancellationToken);

        return Result.Success(composed);
    }
}
