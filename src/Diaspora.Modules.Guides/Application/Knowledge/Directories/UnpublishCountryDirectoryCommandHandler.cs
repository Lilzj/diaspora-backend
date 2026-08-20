using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>Unpublishes a country directory, keeping the arrangement intact.</summary>
public sealed class UnpublishCountryDirectoryCommandHandler
    : ICommandHandler<UnpublishCountryDirectoryCommand>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;

    public UnpublishCountryDirectoryCommandHandler(
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

    public async Task<Result> Handle(
        UnpublishCountryDirectoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var directory = await _directories.GetAsync(command.CountryCode, cancellationToken);

        if (directory is null)
        {
            return Result.Failure(KnowledgeErrors.DirectoryNotFound);
        }

        var unpublished = directory.Unpublish(_authorization.CallerId);

        if (unpublished.IsFailure)
        {
            return unpublished;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(directory.CountryCode, cancellationToken);

        return Result.Success();
    }
}
