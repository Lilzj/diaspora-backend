using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Moderation;

/// <summary>Reads the decision history. What an appeal is answered from.</summary>
public sealed class GetModerationHistoryQueryHandler
    : IQueryHandler<GetModerationHistoryQuery, IReadOnlyList<ModerationDecisionResponse>>
{
    private readonly ModerationService _moderation;
    private readonly ContentReferenceResolver _references;

    public GetModerationHistoryQueryHandler(
        ModerationService moderation, ContentReferenceResolver references)
    {
        _moderation = moderation;
        _references = references;
    }

    public async Task<Result<IReadOnlyList<ModerationDecisionResponse>>> Handle(
        GetModerationHistoryQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reference = _references.Resolve(request.ContentType, request.ContentId);

        if (reference.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ModerationDecisionResponse>>(reference.Error);
        }

        var history = await _moderation.GetHistoryAsync(reference.Value, cancellationToken);

        return Result.Success<IReadOnlyList<ModerationDecisionResponse>>(
            history.Select(ModerationMapper.ToResponse).ToList());
    }
}
