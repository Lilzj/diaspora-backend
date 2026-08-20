using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Rearranges a country's navigation.
/// <para>
/// <b>Every key is verified against the catalogue in one batched read before anything is
/// stored.</b> An entry pointing at a row that does not exist renders as a raw key on the most-read
/// screen in the product, and the composer's only alternative is to drop the line and leave a gap
/// nobody can account for. Checking here — where the curator is present and can be told which key
/// was wrong — is the only place the mistake is cheap.
/// </para>
/// <para>
/// Archived rows are rejected too. Placing one would produce an entry that is invisible to every
/// reader and present in the curator's list, which is the most confusing possible outcome.
/// </para>
/// </summary>
public sealed class ArrangeCountryDirectoryCommandHandler
    : ICommandHandler<ArrangeCountryDirectoryCommand, CountryDirectoryDetailResponse>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;

    public ArrangeCountryDirectoryCommandHandler(
        ICountryKnowledgeDirectoryRepository directories,
        IKnowledgeCategoryRepository categories,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache)
    {
        _directories = directories;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
    }

    public async Task<Result<CountryDirectoryDetailResponse>> Handle(
        ArrangeCountryDirectoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var directory = await _directories.GetAsync(command.CountryCode, cancellationToken);

        if (directory is null)
        {
            return Result.Failure<CountryDirectoryDetailResponse>(
                KnowledgeErrors.DirectoryNotFound);
        }

        var request = command.Request;

        var revision = directory.EnsureRevision(request.Revision);

        if (revision.IsFailure)
        {
            return Result.Failure<CountryDirectoryDetailResponse>(revision.Error);
        }

        var requested = request.Entries
            .Select(entry => entry.KnowledgeCategoryKey)
            .ToList();

        // One batched read for the whole arrangement. A forty-row directory must not be forty
        // queries to save once.
        var known = await _categories.GetByKeysAsync(requested, cancellationToken);

        foreach (var entry in request.Entries)
        {
            var key = KnowledgeNormalization.Key(entry.KnowledgeCategoryKey);

            if (key is null || !known.ContainsKey(key))
            {
                return Result.Failure<CountryDirectoryDetailResponse>(
                    KnowledgeErrors.CategoryNotFound);
            }
        }

        var arranged = directory.Arrange(
            [.. request.Entries.Select(
                entry =>
                    ((string?)entry.KnowledgeCategoryKey, entry.LabelOverride, entry.IsHidden))],
            _authorization.CallerId);

        if (arranged.IsFailure)
        {
            return Result.Failure<CountryDirectoryDetailResponse>(arranged.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(directory.CountryCode, cancellationToken);

        var labels = known.ToDictionary(
            entry => entry.Key, entry => entry.Value.Name, StringComparer.Ordinal);

        return Result.Success(
            CountryDirectoryMapper.ToDetail(
                directory, labels, new Dictionary<Guid, GuideCardResponse>()));
    }
}
