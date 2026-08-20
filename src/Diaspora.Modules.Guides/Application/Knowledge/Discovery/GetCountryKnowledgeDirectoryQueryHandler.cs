using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Serves a country page.
/// <para>
/// <b>Cached, and the view is recorded whether or not it was.</b> Those two are deliberately
/// independent: caching is about not rebuilding the page, and the view counter is about how many
/// people opened it. A cache hit that skipped the counter would make the most popular countries look
/// like the least visited ones, which is precisely backwards.
/// </para>
/// <para>
/// <b>A curator sees an unpublished directory and a reader does not.</b> The check is here rather
/// than in the composer, so there is exactly one place where "may this person see this country"
/// is decided.
/// </para>
/// </summary>
public sealed class GetCountryKnowledgeDirectoryQueryHandler
    : IQueryHandler<GetCountryKnowledgeDirectoryQuery, CountryKnowledgeDirectoryResponse>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly KnowledgeDirectoryComposer _composer;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly KnowledgeAnalyticsRecorder _analytics;
    private readonly IKnowledgeCache _cache;
    private readonly IClock _clock;

    public GetCountryKnowledgeDirectoryQueryHandler(
        ICountryKnowledgeDirectoryRepository directories,
        KnowledgeDirectoryComposer composer,
        KnowledgeAuthorizationService authorization,
        KnowledgeAnalyticsRecorder analytics,
        IKnowledgeCache cache,
        IClock clock)
    {
        _directories = directories;
        _composer = composer;
        _authorization = authorization;
        _analytics = analytics;
        _cache = cache;
        _clock = clock;
    }

    public async Task<Result<CountryKnowledgeDirectoryResponse>> Handle(
        GetCountryKnowledgeDirectoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var directory = await _directories.GetForReadAsync(
            query.CountryCode, cancellationToken);

        if (directory is null)
        {
            return Result.Failure<CountryKnowledgeDirectoryResponse>(
                KnowledgeErrors.DirectoryNotFound);
        }

        if (!directory.IsPublished && !_authorization.IsCurator)
        {
            // Not-found rather than forbidden: that a country is being prepared is itself something
            // a reader has no business learning from a 403.
            return Result.Failure<CountryKnowledgeDirectoryResponse>(
                KnowledgeErrors.DirectoryNotFound);
        }

        var composed = await ComposeAsync(query, directory, cancellationToken);

        await RecordViewAsync(directory.CountryCode, query.Locale, cancellationToken);

        return Result.Success(composed);
    }

    /// <summary>
    /// Builds the page, from cache when it is a reader's and the language filter is the default one.
    /// <para>
    /// A curator's view is never cached: they read it immediately after a write, and a cache would
    /// show them their own edit not having happened. A language-filtered view is not cached either —
    /// it is a rare request and caching it would multiply the key space by every language on the
    /// platform for very little return.
    /// </para>
    /// </summary>
    private async Task<CountryKnowledgeDirectoryResponse> ComposeAsync(
        GetCountryKnowledgeDirectoryQuery query,
        CountryKnowledgeDirectory directory,
        CancellationToken cancellationToken)
    {
        var cacheable = directory.IsPublished
            && !_authorization.IsCurator
            && string.IsNullOrWhiteSpace(query.Language);

        if (!cacheable)
        {
            return await _composer.ComposeAsync(
                directory, query.Locale, query.Language, cancellationToken);
        }

        var key = KnowledgeCacheKeys.Directory(directory.CountryCode, query.Locale);

        var cached = await _cache.GetAsync<CountryKnowledgeDirectoryResponse>(
            key, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        var composed = await _composer.ComposeAsync(
            directory, query.Locale, query.Language, cancellationToken);

        await _cache.SetAsync(key, composed, cancellationToken: cancellationToken);

        return composed;
    }

    /// <summary>
    /// Records the view, on a cache hit as well as a miss.
    /// <para>
    /// A curator's own visits are not counted. Country interest is a product signal, and the team
    /// checking their work every few minutes during a launch would be the largest number on it.
    /// </para>
    /// </summary>
    private async Task RecordViewAsync(
        string countryCode, string? locale, CancellationToken cancellationToken)
    {
        if (_authorization.IsCurator)
        {
            return;
        }

        await _analytics.RecordAsync(
            new KnowledgeAnalyticsSignal
            {
                Kind = KnowledgeAnalyticsSignalKinds.DirectoryViewed,
                CountryCode = countryCode,
                Locale = locale,
                At = _clock.UtcNow
            },
            cancellationToken);
    }
}
