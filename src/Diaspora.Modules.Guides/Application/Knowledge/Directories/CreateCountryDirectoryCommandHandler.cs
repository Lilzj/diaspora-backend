using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Opens a country directory and arranges it from a template.
/// <para>
/// The existence check includes archived directories. An archived country still holds its code, the
/// unique index would refuse a second, and creating a fresh one would strand the archived
/// arrangement beyond any restore — which is a curator's work quietly lost.
/// </para>
/// </summary>
public sealed class CreateCountryDirectoryCommandHandler
    : ICommandHandler<CreateCountryDirectoryCommand, CountryDirectoryDetailResponse>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly CountryDirectoryTemplateResolver _templates;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateCountryDirectoryCommandHandler(
        ICountryKnowledgeDirectoryRepository directories,
        CountryDirectoryTemplateResolver templates,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache,
        IGuidGenerator guids,
        IClock clock)
    {
        _directories = directories;
        _templates = templates;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<CountryDirectoryDetailResponse>> Handle(
        CreateCountryDirectoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await _directories.ExistsAsync(command.CountryCode, cancellationToken))
        {
            return Result.Failure<CountryDirectoryDetailResponse>(KnowledgeErrors.DirectoryExists);
        }

        var caller = _authorization.CallerId;
        var request = command.Request;

        var created = CountryKnowledgeDirectory.Create(
            CountryKnowledgeDirectoryId.From(_guids.NewSequentialGuid()),
            command.CountryCode,
            request.DisplayName,
            _clock.UtcNow,
            request.Summary,
            request.PrimaryLanguage,
            request.Languages,
            caller);

        if (created.IsFailure)
        {
            return Result.Failure<CountryDirectoryDetailResponse>(created.Error);
        }

        var directory = created.Value;

        await _templates.ApplyAsync(directory, caller, cancellationToken);

        await _directories.AddAsync(directory, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(directory.CountryCode, cancellationToken);

        return Result.Success(
            CountryDirectoryMapper.ToDetail(
                directory,
                labels: new Dictionary<string, string>(StringComparer.Ordinal),
                cards: new Dictionary<Guid, GuideCardResponse>()));
    }
}
