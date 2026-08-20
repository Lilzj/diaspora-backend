using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>
/// Publishes, archives or restores a learning path.
/// <para>
/// Every transition is the aggregate's decision — this handler loads, dispatches and saves. That is
/// what keeps "a path needs a title and at least one step to be published" a domain rule enforceable
/// from a seeder, a migration or a future admin tool, rather than an HTTP concern that only holds
/// when the request came through this endpoint.
/// </para>
/// </summary>
public sealed class ChangeLearningPathStatusCommandHandler
    : ICommandHandler<ChangeLearningPathStatusCommand>
{
    private readonly ILearningPathRepository _paths;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;
    private readonly IClock _clock;

    public ChangeLearningPathStatusCommandHandler(
        ILearningPathRepository paths,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache,
        IClock clock)
    {
        _paths = paths;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
        _clock = clock;
    }

    public async Task<Result> Handle(
        ChangeLearningPathStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var path = await _paths.GetByKeyAsync(
            command.CountryCode, command.Key, cancellationToken);

        if (path is null)
        {
            return Result.Failure(KnowledgeErrors.LearningPathNotFound);
        }

        var caller = _authorization.CallerId;

        var changed = command.Status switch
        {
            LearningPathStatus.Published => path.Publish(_clock.UtcNow, caller),
            LearningPathStatus.Archived => path.Archive(caller),
            _ => path.Restore(caller)
        };

        if (changed.IsFailure)
        {
            return changed;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(path.CountryCode, cancellationToken);

        return Result.Success();
    }
}
