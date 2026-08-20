using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>
/// Serves one onboarding route.
/// <para>
/// <b>Guides for every step are resolved in a single batched call.</b> A seven-step path resolving
/// one guide per step would be seven queries to render one screen, and the screen is the first thing
/// a newcomer sees.
/// </para>
/// <para>
/// A draft or archived path is visible only to a curator. A newcomer following a link to a
/// half-written sequence is the failure mode the draft state exists to prevent.
/// </para>
/// </summary>
public sealed class GetLearningPathQueryHandler
    : IQueryHandler<GetLearningPathQuery, LearningPathResponse>
{
    private readonly ILearningPathRepository _paths;
    private readonly KnowledgeCardResolver _cards;
    private readonly KnowledgeAuthorizationService _authorization;

    public GetLearningPathQueryHandler(
        ILearningPathRepository paths,
        KnowledgeCardResolver cards,
        KnowledgeAuthorizationService authorization)
    {
        _paths = paths;
        _cards = cards;
        _authorization = authorization;
    }

    public async Task<Result<LearningPathResponse>> Handle(
        GetLearningPathQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var path = await _paths.GetForReadAsync(
            query.CountryCode, query.Key, cancellationToken);

        if (path is null)
        {
            return Result.Failure<LearningPathResponse>(KnowledgeErrors.LearningPathNotFound);
        }

        if (path.Status != LearningPathStatus.Published && !_authorization.IsCurator)
        {
            // Not-found rather than forbidden: the existence of an unpublished path is itself
            // something a reader has no business learning.
            return Result.Failure<LearningPathResponse>(KnowledgeErrors.LearningPathNotFound);
        }

        var cards = await _cards.ResolveAsync(
            LearningPathMapper.GuideIdsOf(path), cancellationToken);

        return Result.Success(LearningPathMapper.ToResponse(path, cards));
    }
}
