using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>Rewrites a path's copy, leaving its steps alone.</summary>
public sealed class UpdateLearningPathCommandHandler
    : ICommandHandler<UpdateLearningPathCommand, LearningPathResponse>
{
    private readonly ILearningPathRepository _paths;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;

    public UpdateLearningPathCommandHandler(
        ILearningPathRepository paths,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache)
    {
        _paths = paths;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
    }

    public async Task<Result<LearningPathResponse>> Handle(
        UpdateLearningPathCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var path = await _paths.GetByKeyAsync(
            command.CountryCode, command.Key, cancellationToken);

        if (path is null)
        {
            return Result.Failure<LearningPathResponse>(KnowledgeErrors.LearningPathNotFound);
        }

        var revision = path.EnsureRevision(command.Request.Revision);

        if (revision.IsFailure)
        {
            return Result.Failure<LearningPathResponse>(revision.Error);
        }

        var updated = path.UpdateMetadata(
            command.Request.Title,
            command.Request.Description,
            command.Request.Language,
            _authorization.CallerId);

        if (updated.IsFailure)
        {
            return Result.Failure<LearningPathResponse>(updated.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(path.CountryCode, cancellationToken);

        return Result.Success(
            LearningPathMapper.ToResponse(path, new Dictionary<Guid, GuideCardResponse>()));
    }
}
