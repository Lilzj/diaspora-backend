using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Recommendations;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Modules.Guides.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Knowledge.Curation;

/// <summary>
/// Replaces the curated next reads for one guide.
/// <para>
/// <b>The source guide must exist and be published; the recommended ones must too.</b> A "read next"
/// pointing at a draft is a dead link at the end of the guide somebody just finished, which is the
/// worst possible moment to hand them one. Every identifier is resolved in a single batched call
/// rather than one at a time.
/// </para>
/// <para>
/// The aggregate is created on first use. There is no separate "start recommending" step, because
/// there is nothing to decide: an editor setting a list is the only thing that ever brings one into
/// existence.
/// </para>
/// </summary>
public sealed class SetRecommendedReadsCommandHandler : ICommandHandler<SetRecommendedReadsCommand>
{
    private readonly IKnowledgeRecommendationRepository _recommendations;
    private readonly IGuideDirectory _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SetRecommendedReadsCommandHandler(
        IKnowledgeRecommendationRepository recommendations,
        IGuideDirectory guides,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache,
        IGuidGenerator guids,
        IClock clock)
    {
        _recommendations = recommendations;
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result> Handle(
        SetRecommendedReadsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var sourceId = GuideId.From(command.GuideId);

        if (!await _guides.ExistsAsync(sourceId, cancellationToken))
        {
            return Result.Failure(KnowledgeErrors.GuideNotDiscoverable);
        }

        var requested = command.Request.Reads;

        if (requested.Count > 0)
        {
            // One batched resolution for the whole list. A twelve-entry set must not be twelve
            // round trips to save once.
            var resolved = await _guides.FindManyAsync(
                [.. requested.Select(read => GuideId.From(read.GuideId))], cancellationToken);

            if (requested.Any(read => !resolved.ContainsKey(read.GuideId)))
            {
                return Result.Failure(KnowledgeErrors.GuideNotDiscoverable);
            }
        }

        var recommendation = await _recommendations.GetBySourceAsync(sourceId, cancellationToken);

        if (recommendation is null)
        {
            var created = KnowledgeRecommendation.Create(
                KnowledgeRecommendationId.From(_guids.NewSequentialGuid()),
                sourceId,
                _clock.UtcNow);

            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            recommendation = created.Value;

            await _recommendations.AddAsync(recommendation, cancellationToken);
        }
        else
        {
            var revision = recommendation.EnsureRevision(command.Request.Revision);

            if (revision.IsFailure)
            {
                return revision;
            }
        }

        var applied = recommendation.SetReads(
            [.. requested.Select(read => (read.GuideId, read.Reason))], caller.Value);

        if (applied.IsFailure)
        {
            return applied;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateAsync(
            KnowledgeCacheKeys.GuideScope(command.GuideId), cancellationToken);

        return Result.Success();
    }
}
