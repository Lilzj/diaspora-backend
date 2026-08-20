using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>Publishes a country directory.</summary>
public sealed class PublishCountryDirectoryCommandHandler
    : ICommandHandler<PublishCountryDirectoryCommand>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;
    private readonly IClock _clock;

    public PublishCountryDirectoryCommandHandler(
        ICountryKnowledgeDirectoryRepository directories,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache,
        IClock clock)
    {
        _directories = directories;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
        _clock = clock;
    }

    public async Task<Result> Handle(
        PublishCountryDirectoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var directory = await _directories.GetAsync(command.CountryCode, cancellationToken);

        if (directory is null)
        {
            return Result.Failure(KnowledgeErrors.DirectoryNotFound);
        }

        var published = directory.Publish(_clock.UtcNow, _authorization.CallerId);

        if (published.IsFailure)
        {
            return published;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(directory.CountryCode, cancellationToken);

        return Result.Success();
    }
}
