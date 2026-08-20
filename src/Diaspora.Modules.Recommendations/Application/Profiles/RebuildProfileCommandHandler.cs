using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Application.Profiles;

/// <summary>
/// Recomputes the caller's profile, no more often than the cooldown allows.
/// <para>
/// <b>The cooldown is enforced here rather than by a rate limiter, and the difference is the error
/// message.</b> A generic 429 tells a member they are doing something too much; this tells them
/// their profile was rebuilt two minutes ago and is already current, which is both true and the
/// answer they actually needed.
/// </para>
/// </summary>
public sealed class RebuildProfileCommandHandler
    : ICommandHandler<RebuildProfileCommand, PersonalizationProfileResponse>
{
    private readonly IPersonalizationProfileBuilder _builder;
    private readonly IPersonalizationProfileRepository _profiles;
    private readonly RecommendationAuthorizationService _authorization;
    private readonly IOptions<RecommendationOptions> _options;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RebuildProfileCommandHandler(
        IPersonalizationProfileBuilder builder,
        IPersonalizationProfileRepository profiles,
        RecommendationAuthorizationService authorization,
        IOptions<RecommendationOptions> options,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _builder = builder;
        _profiles = profiles;
        _authorization = authorization;
        _options = options;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PersonalizationProfileResponse>> Handle(
        RebuildProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<PersonalizationProfileResponse>(caller.Error);
        }

        var existing = await _profiles.FindAsync(caller.Value, cancellationToken);
        var cooldown = _options.Value.RebuildCooldown;

        // A specific answer rather than a generic 429. See the class summary.
        if (existing?.LastRebuiltAt is { } lastRebuiltAt &&
            _clock.UtcNow - lastRebuiltAt < cooldown)
        {
            var remaining = cooldown - (_clock.UtcNow - lastRebuiltAt);

            return Result.Failure<PersonalizationProfileResponse>(Error.RateLimited(
                RecommendationErrorCodes.ProfileRebuildThrottled,
                $"Your recommendations were rebuilt recently and are already current. "
                + $"Try again in {Math.Ceiling(remaining.TotalMinutes)} minute(s)."));
        }

        var profile = await _builder.RebuildAsync(caller.Value, cancellationToken);

        // A rebuild empties and repopulates. A partial one is worse than none.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(RecommendationMapper.ToResponse(profile));
    }
}
