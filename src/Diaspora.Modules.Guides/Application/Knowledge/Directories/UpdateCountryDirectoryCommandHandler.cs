using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Rewrites a country directory's header.
/// <para>
/// The revision is checked before anything is mutated, and the country cache is dropped afterwards:
/// the header appears on the country page, the dashboard and every cached locale variant of both.
/// </para>
/// </summary>
public sealed class UpdateCountryDirectoryCommandHandler
    : ICommandHandler<UpdateCountryDirectoryCommand, CountryDirectoryDetailResponse>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;

    public UpdateCountryDirectoryCommandHandler(
        ICountryKnowledgeDirectoryRepository directories,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache)
    {
        _directories = directories;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
    }

    public async Task<Result<CountryDirectoryDetailResponse>> Handle(
        UpdateCountryDirectoryCommand command, CancellationToken cancellationToken)
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

        var caller = _authorization.CallerId;

        var updated = directory.UpdateMetadata(
            request.DisplayName,
            request.Summary,
            request.PrimaryLanguage,
            request.Languages,
            caller);

        if (updated.IsFailure)
        {
            return Result.Failure<CountryDirectoryDetailResponse>(updated.Error);
        }

        var localizations = directory.SetLocalizations(
            [.. request.Localizations.Select(
                localization =>
                    ((string?)localization.Locale,
                        (string?)localization.Name,
                        localization.Description))],
            caller);

        if (localizations.IsFailure)
        {
            return Result.Failure<CountryDirectoryDetailResponse>(localizations.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(directory.CountryCode, cancellationToken);

        return Result.Success(
            CountryDirectoryMapper.ToDetail(
                directory,
                labels: new Dictionary<string, string>(StringComparer.Ordinal),
                cards: new Dictionary<Guid, GuideCardResponse>()));
    }
}
