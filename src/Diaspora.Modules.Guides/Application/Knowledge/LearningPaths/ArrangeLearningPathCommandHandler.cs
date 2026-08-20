using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Modules.Guides.Integration;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>
/// Rearranges a path's steps.
/// <para>
/// <b>Every named guide is resolved in one batched call before anything is stored.</b> A step
/// pointing at a draft or a withdrawn guide is a dead link inside a numbered sequence a newcomer is
/// following, which is the worst place on the platform to put one — and checking here is the only
/// moment the curator is present to be told which step is wrong.
/// </para>
/// <para>
/// Step identifiers are caller-supplied and preserved by the aggregate, so a client tracking "you
/// are on step 3" still knows which step that was after an insertion above it.
/// </para>
/// </summary>
public sealed class ArrangeLearningPathCommandHandler
    : ICommandHandler<ArrangeLearningPathCommand, LearningPathResponse>
{
    private readonly ILearningPathRepository _paths;
    private readonly IGuideDirectory _guides;
    private readonly KnowledgeCardResolver _cards;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;

    public ArrangeLearningPathCommandHandler(
        ILearningPathRepository paths,
        IGuideDirectory guides,
        KnowledgeCardResolver cards,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache)
    {
        _paths = paths;
        _guides = guides;
        _cards = cards;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
    }

    public async Task<Result<LearningPathResponse>> Handle(
        ArrangeLearningPathCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var path = await _paths.GetByKeyAsync(
            command.CountryCode, command.Key, cancellationToken);

        if (path is null)
        {
            return Result.Failure<LearningPathResponse>(KnowledgeErrors.LearningPathNotFound);
        }

        var request = command.Request;

        var revision = path.EnsureRevision(request.Revision);

        if (revision.IsFailure)
        {
            return Result.Failure<LearningPathResponse>(revision.Error);
        }

        var namedGuides = request.Steps
            .Select(step => step.GuideId)
            .OfType<Guid>()
            .Where(guideId => guideId != Guid.Empty)
            .Distinct()
            .ToList();

        if (namedGuides.Count > 0)
        {
            var resolved = await _guides.FindManyAsync(
                [.. namedGuides.Select(GuideId.From)], cancellationToken);

            if (namedGuides.Any(guideId => !resolved.ContainsKey(guideId)))
            {
                return Result.Failure<LearningPathResponse>(
                    KnowledgeErrors.GuideNotDiscoverable);
            }
        }

        var arranged = path.Arrange(
            [.. request.Steps.Select(step =>
                (step.StepId,
                    (string?)step.Title,
                    step.Description,
                    step.GuideId,
                    step.KnowledgeCategoryKey,
                    step.IsOptional))],
            _authorization.CallerId);

        if (arranged.IsFailure)
        {
            return Result.Failure<LearningPathResponse>(arranged.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(path.CountryCode, cancellationToken);

        // The arrangement screen renders this response next, and a curator who has just attached a
        // guide to step 4 needs to see which guide it was. Returning identifiers and making the
        // client re-fetch would be a round trip for information this handler already resolved.
        var cards = await _cards.ResolveAsync(
            LearningPathMapper.GuideIdsOf(path), cancellationToken);

        return Result.Success(LearningPathMapper.ToResponse(path, cards));
    }
}
