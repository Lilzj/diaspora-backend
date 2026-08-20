using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// A moderator approving, rejecting or retiring an insight.
/// <para>
/// <b>The profile's published count is recomputed rather than incremented.</b> B1.1's rule for an
/// at-least-once world taken as written: an increment applied twice on a redelivery drifts upward
/// forever, and a count nobody can reconcile is worse than no count. Recomputing costs one query on
/// a path a moderator hits a few dozen times a day.
/// </para>
/// </summary>
public sealed class DecideNeighborhoodInsightCommandHandler
    : ICommandHandler<DecideNeighborhoodInsightCommand, NeighborhoodInsightResponse>
{
    private readonly INeighborhoodInsightRepository _insights;
    private readonly INeighborhoodProfileRepository _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IClock _clock;

    public DecideNeighborhoodInsightCommandHandler(
        INeighborhoodInsightRepository insights,
        INeighborhoodProfileRepository profiles,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IClock clock)
    {
        _insights = insights;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<NeighborhoodInsightResponse>> Handle(
        DecideNeighborhoodInsightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NeighborhoodInsightResponse>(caller.Error);
        }

        var insight = await _insights.GetAsync(
            NeighborhoodInsightId.From(command.InsightId), cancellationToken);

        if (insight is null)
        {
            return Result.Failure<NeighborhoodInsightResponse>(HousingErrors.InsightNotFound);
        }

        var now = _clock.UtcNow;
        var note = command.Request.Note;

        var decided = command.Request.Action?.Trim().ToLowerInvariant() switch
        {
            "approve" => insight.Approve(caller.Value, note, now),
            "reject" => insight.Reject(caller.Value, note, now),
            "retire" => insight.Retire(caller.Value, note, now),
            _ => Result.Failure(HousingErrors.InsightAlreadyDecided)
        };

        if (decided.IsFailure)
        {
            return Result.Failure<NeighborhoodInsightResponse>(decided.Error);
        }

        await RefreshProfileCountAsync(insight, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (insight.IsPublished)
        {
            HousingTelemetry.InsightsApproved.Add(
                1,
                new KeyValuePair<string, object?>("category", insight.Category.ToString()));
        }

        return Result.Success(NeighborhoodMapper.ToResponse(
            insight, caller.Value, isStaff: true, hasEndorsed: false));
    }

    /// <summary>
    /// Recomputes the profile's published-insight count. See the class summary for why it is set
    /// rather than incremented.
    /// </summary>
    private async Task RefreshProfileCountAsync(
        NeighborhoodInsight insight, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(insight.ProfileId, cancellationToken);

        if (profile is null)
        {
            return;
        }

        var approved = await _insights.CountApprovedForAreaAsync(
            insight.AreaId, cancellationToken);

        profile.SetApprovedInsightCount(approved);
    }
}
