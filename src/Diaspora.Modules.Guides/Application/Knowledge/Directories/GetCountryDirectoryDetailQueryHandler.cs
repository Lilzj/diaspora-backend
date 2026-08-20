using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Serves the curation screen for one country.
/// <para>
/// <b>Never cached.</b> A curator reads this immediately after every write, and a cache would show
/// them their own edit not having happened — which is the single most confusing thing a curation
/// tool can do.
/// </para>
/// <para>
/// <b>Featured guides are resolved, and the ones that no longer resolve are exactly the point.</b> A
/// row whose guide has been unpublished comes back with a null card, because that is the row the
/// curator needs to see and remove. The reader-facing page silently drops it; this one does not.
/// </para>
/// </summary>
public sealed class GetCountryDirectoryDetailQueryHandler
    : IQueryHandler<GetCountryDirectoryDetailQuery, CountryDirectoryDetailResponse>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly KnowledgeCardResolver _cards;

    public GetCountryDirectoryDetailQueryHandler(
        ICountryKnowledgeDirectoryRepository directories,
        IKnowledgeCategoryRepository categories,
        KnowledgeCardResolver cards)
    {
        _directories = directories;
        _categories = categories;
        _cards = cards;
    }

    public async Task<Result<CountryDirectoryDetailResponse>> Handle(
        GetCountryDirectoryDetailQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var directory = query.IncludeArchived
            ? await _directories.GetIncludingArchivedAsync(query.CountryCode, cancellationToken)
            : await _directories.GetForReadAsync(query.CountryCode, cancellationToken);

        if (directory is null)
        {
            return Result.Failure<CountryDirectoryDetailResponse>(
                KnowledgeErrors.DirectoryNotFound);
        }

        var labels = (await _categories.GetByKeysAsync(
                [.. directory.Entries.Select(entry => entry.KnowledgeCategoryKey)],
                cancellationToken))
            .ToDictionary(entry => entry.Key, entry => entry.Value.Name, StringComparer.Ordinal);

        var cards = await _cards.ResolveAsync(
            [.. directory.FeaturedGuides.Select(featured => featured.GuideId)], cancellationToken);

        return Result.Success(CountryDirectoryMapper.ToDetail(directory, labels, cards));
    }
}
