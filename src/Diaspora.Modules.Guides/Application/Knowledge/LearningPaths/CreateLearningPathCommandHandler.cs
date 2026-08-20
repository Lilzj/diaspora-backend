using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>
/// Writes a new learning path as a draft.
/// <para>
/// The key check is advisory — the unique index over (country, key) is authoritative — but it turns
/// the ordinary case, a curator reusing a key, into a clear conflict rather than a constraint
/// violation and a rolled-back transaction.
/// </para>
/// </summary>
public sealed class CreateLearningPathCommandHandler
    : ICommandHandler<CreateLearningPathCommand, LearningPathResponse>
{
    private readonly ILearningPathRepository _paths;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateLearningPathCommandHandler(
        ILearningPathRepository paths,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache,
        IGuidGenerator guids,
        IClock clock)
    {
        _paths = paths;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<LearningPathResponse>> Handle(
        CreateLearningPathCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (await _paths.KeyExistsAsync(command.CountryCode, request.Key, cancellationToken))
        {
            return Result.Failure<LearningPathResponse>(KnowledgeErrors.PathKeyTaken);
        }

        var created = LearningPath.Create(
            LearningPathId.From(_guids.NewSequentialGuid()),
            request.Key,
            command.CountryCode,
            request.Title,
            _clock.UtcNow,
            request.Description,
            request.Language,
            _authorization.CallerId);

        if (created.IsFailure)
        {
            return Result.Failure<LearningPathResponse>(created.Error);
        }

        await _paths.AddAsync(created.Value, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(created.Value.CountryCode, cancellationToken);

        return Result.Success(
            LearningPathMapper.ToResponse(
                created.Value, new Dictionary<Guid, GuideCardResponse>()));
    }
}
