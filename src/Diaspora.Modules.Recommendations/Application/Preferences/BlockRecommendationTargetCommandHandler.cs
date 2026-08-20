using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Preferences;

/// <summary>Blocks or unblocks a target for the caller.</summary>
public sealed class BlockRecommendationTargetCommandHandler
    : ICommandHandler<BlockRecommendationTargetCommand, PersonalizationPreferencesResponse>
{
    private readonly IPersonalizationPreferenceRepository _preferences;
    private readonly RecommendationAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public BlockRecommendationTargetCommandHandler(
        IPersonalizationPreferenceRepository preferences,
        RecommendationAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _preferences = preferences;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PersonalizationPreferencesResponse>> Handle(
        BlockRecommendationTargetCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<PersonalizationPreferencesResponse>(caller.Error);
        }

        var targetType = RecommendationAuthorizationService.ParseTargetType(command.TargetType);

        if (targetType.IsFailure)
        {
            return Result.Failure<PersonalizationPreferencesResponse>(targetType.Error);
        }

        var preference = await _preferences.GetOrCreateAsync(caller.Value, cancellationToken);

        if (command.Remove)
        {
            preference.UnblockTarget(targetType.Value, command.TargetId);
        }
        else
        {
            preference.BlockTarget(
                targetType.Value, command.TargetId, _clock.UtcNow, _guids.NewSequentialGuid());
        }

        _preferences.Update(preference);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(RecommendationMapper.ToResponse(preference));
    }
}
