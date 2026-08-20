using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Archives a country directory, through the platform's soft-delete path.
/// <para>
/// The handler marks the aggregate for removal and saves; the shared context converts that into a
/// soft delete, stamps the timestamp and the actor, and leaves the row where a restore can find it.
/// Stamping it by hand is the defect B1.0.1 records, and it is silent.
/// </para>
/// </summary>
public sealed class ArchiveCountryDirectoryCommandHandler
    : ICommandHandler<ArchiveCountryDirectoryCommand>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKnowledgeCache _cache;

    public ArchiveCountryDirectoryCommandHandler(
        ICountryKnowledgeDirectoryRepository directories,
        IUnitOfWork unitOfWork,
        IKnowledgeCache cache)
    {
        _directories = directories;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<Result> Handle(
        ArchiveCountryDirectoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var directory = await _directories.GetAsync(command.CountryCode, cancellationToken);

        if (directory is null)
        {
            return Result.Failure(KnowledgeErrors.DirectoryNotFound);
        }

        _directories.Remove(directory);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(directory.CountryCode, cancellationToken);

        return Result.Success();
    }
}
