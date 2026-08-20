using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Profiles;

/// <summary>Folds the caller's pending signals into their profile and returns the result.</summary>
public sealed class RefreshProfileCommandHandler
    : ICommandHandler<RefreshProfileCommand, PersonalizationProfileResponse>
{
    private readonly IPersonalizationProfileBuilder _builder;
    private readonly RecommendationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshProfileCommandHandler(
        IPersonalizationProfileBuilder builder,
        RecommendationAuthorizationService authorization,
        IUnitOfWork unitOfWork)
    {
        _builder = builder;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PersonalizationProfileResponse>> Handle(
        RefreshProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<PersonalizationProfileResponse>(caller.Error);
        }

        var profile = await _builder.BuildIncrementalAsync(caller.Value, cancellationToken);

        // The folded profile and the processed stamps together — a save that persisted one without
        // the other would fold the same signals again on the next pass.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(RecommendationMapper.ToResponse(profile));
    }
}
