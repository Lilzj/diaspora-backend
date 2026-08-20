using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Signs a neighbourhood profile off for publication, and stamps the review date.
/// <para>
/// The review stamp is what lets the platform find profiles nobody has looked at since the tram
/// line opened. Prose about a place is a living summary rather than a fact, and one that is quietly
/// out of date is worse than one that is missing.
/// </para>
/// </summary>
public sealed class ApproveNeighborhoodProfileCommandHandler
    : ICommandHandler<ApproveNeighborhoodProfileCommand, NeighborhoodProfileResponse>
{
    private readonly INeighborhoodProfileRepository _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NeighborhoodStatisticsService _statistics;
    private readonly HousingAuthorizationService _authorization;
    private readonly IOptions<HousingOptions> _options;
    private readonly IClock _clock;

    public ApproveNeighborhoodProfileCommandHandler(
        INeighborhoodProfileRepository profiles,
        IUnitOfWork unitOfWork,
        NeighborhoodStatisticsService statistics,
        HousingAuthorizationService authorization,
        IOptions<HousingOptions> options,
        IClock clock)
    {
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _statistics = statistics;
        _authorization = authorization;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<NeighborhoodProfileResponse>> Handle(
        ApproveNeighborhoodProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NeighborhoodProfileResponse>(caller.Error);
        }

        var profile = await _profiles.GetByAreaAsync(command.AreaId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<NeighborhoodProfileResponse>(
                HousingErrors.NeighborhoodProfileNotFound);
        }

        var now = _clock.UtcNow;
        var approved = profile.Approve(caller.Value, now);

        if (approved.IsFailure)
        {
            return Result.Failure<NeighborhoodProfileResponse>(approved.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var statistics = await _statistics.ForAsync(profile, cancellationToken);

        return Result.Success(NeighborhoodMapper.ToResponse(
            profile,
            statistics,
            [],
            now,
            _options.Value.NeighborhoodReviewIntervalDays));
    }
}
