using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Records what a member knows about a place, for a moderator to read.
/// <para>
/// <b>Nothing published here is visible to anybody until a moderator approves it</b>, and the
/// duplicate check is the reason members do not simply resubmit until something appears. Comparing
/// on the normalised fingerprint catches the same observation retyped with different spacing, which
/// is exactly what an impatient member does when their first attempt has not shown up — because it
/// is waiting in a queue they cannot see.
/// </para>
/// <para>
/// <b>The profile is created on demand.</b> A member is often the first person ever to say anything
/// about their neighbourhood, and requiring an editor to have made a profile first would lose
/// precisely the contributions worth having.
/// </para>
/// </summary>
public sealed class SubmitNeighborhoodInsightCommandHandler
    : ICommandHandler<SubmitNeighborhoodInsightCommand, NeighborhoodInsightResponse>
{
    private readonly INeighborhoodInsightRepository _insights;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingNeighborhoodResolver _neighborhoods;
    private readonly HousingAuthorizationService _authorization;
    private readonly IOptions<HousingOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SubmitNeighborhoodInsightCommandHandler(
        INeighborhoodInsightRepository insights,
        IUnitOfWork unitOfWork,
        HousingNeighborhoodResolver neighborhoods,
        HousingAuthorizationService authorization,
        IOptions<HousingOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _insights = insights;
        _unitOfWork = unitOfWork;
        _neighborhoods = neighborhoods;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<NeighborhoodInsightResponse>> Handle(
        SubmitNeighborhoodInsightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_options.Value.EnableCommunityInsights)
        {
            // A deployment launching in a new market with no moderator coverage turns this off
            // rather than accumulating an unreviewed queue of claims about real places.
            return Result.Failure<NeighborhoodInsightResponse>(HousingErrors.InsightNotFound);
        }

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NeighborhoodInsightResponse>(caller.Error);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        var submittedToday = await _insights.CountByAuthorSinceAsync(
            caller.Value, now.AddDays(-1), cancellationToken);

        if (submittedToday >= _options.Value.MaxInsightsPerAuthorPerDay)
        {
            return Result.Failure<NeighborhoodInsightResponse>(HousingErrors.TooManyInsights);
        }

        var fingerprint = NeighborhoodInsight.Fingerprints(request.Body);

        if (await _insights.HasDuplicateAsync(
                request.AreaId, caller.Value, fingerprint, cancellationToken))
        {
            return Result.Failure<NeighborhoodInsightResponse>(HousingErrors.InsightDuplicate);
        }

        // Created on demand. See the class summary.
        var profile = await _neighborhoods.GetOrCreateProfileAsync(
            request.AreaId, caller.Value, cancellationToken);

        if (profile.IsFailure)
        {
            return Result.Failure<NeighborhoodInsightResponse>(profile.Error);
        }

        if (!Enum.TryParse<NeighborhoodInsightCategory>(
                request.Category, ignoreCase: true, out var category))
        {
            category = NeighborhoodInsightCategory.Other;
        }

        var insight = NeighborhoodInsight.Submit(
            NeighborhoodInsightId.From(_guids.NewSequentialGuid()),
            profile.Value.Id,
            request.AreaId,
            caller.Value,
            category,
            request.Body,
            request.Label,
            now,

            // The neighbourhood's centre, so the area around a point finds this insight too.
            profile.Value.Latitude,
            profile.Value.Longitude);

        if (insight.IsFailure)
        {
            return Result.Failure<NeighborhoodInsightResponse>(insight.Error);
        }

        await _insights.AddAsync(insight.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        HousingTelemetry.InsightsSubmitted.Add(
            1, new KeyValuePair<string, object?>("category", category.ToString()));

        return Result.Success(NeighborhoodMapper.ToResponse(
            insight.Value, caller.Value, _authorization.IsStaff, hasEndorsed: false));
    }
}
