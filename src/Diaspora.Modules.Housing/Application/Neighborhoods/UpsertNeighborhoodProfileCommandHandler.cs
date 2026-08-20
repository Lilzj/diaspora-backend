using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Writes or revises a neighbourhood profile.
/// <para>
/// <b>Creates the profile on demand if this is the first time anybody has described the place.</b>
/// That is deliberate: a profile is not something an editor has to remember to make first, and the
/// alternative — a create endpoint followed by an update — would mean the platform only ever had
/// profiles for the places somebody already cared about rather than for the places members live.
/// </para>
/// <para>
/// <b>Writing unapproves.</b> An editor's change lands with <c>IsApproved</c> false and stays there
/// until somebody with the approval permission signs it off, which is what makes these summaries
/// safe to let a wide set of contributors edit. Refreshing the cached geography from B4 does not
/// unapprove, because a place being renamed says nothing about whether the prose is still true.
/// </para>
/// </summary>
public sealed class UpsertNeighborhoodProfileCommandHandler
    : ICommandHandler<UpsertNeighborhoodProfileCommand, NeighborhoodProfileResponse>
{
    private readonly INeighborhoodProfileRepository _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingNeighborhoodResolver _neighborhoods;
    private readonly NeighborhoodStatisticsService _statistics;
    private readonly HousingAuthorizationService _authorization;
    private readonly IOptions<HousingOptions> _options;
    private readonly IClock _clock;

    public UpsertNeighborhoodProfileCommandHandler(
        INeighborhoodProfileRepository profiles,
        IUnitOfWork unitOfWork,
        HousingNeighborhoodResolver neighborhoods,
        NeighborhoodStatisticsService statistics,
        HousingAuthorizationService authorization,
        IOptions<HousingOptions> options,
        IClock clock)
    {
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _neighborhoods = neighborhoods;
        _statistics = statistics;
        _authorization = authorization;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<NeighborhoodProfileResponse>> Handle(
        UpsertNeighborhoodProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var caller = _authorization.CallerId;

        // Created on demand. See the class summary.
        var profile = await _neighborhoods.GetOrCreateProfileAsync(
            request.AreaId, caller, cancellationToken);

        if (profile.IsFailure)
        {
            return Result.Failure<NeighborhoodProfileResponse>(profile.Error);
        }

        var now = _clock.UtcNow;

        var narrative = NeighborhoodNarrative.Create(
            request.Description ?? profile.Value.Narrative.Description,
            request.Overview ?? profile.Value.Narrative.Overview,
            request.SafetySummary ?? profile.Value.Narrative.SafetySummary,
            request.TransportSummary ?? profile.Value.Narrative.TransportSummary,
            request.CostOfLivingSummary ?? profile.Value.Narrative.CostOfLivingSummary,
            request.CommunitySummary ?? profile.Value.Narrative.CommunitySummary,
            request.ClimateNotes ?? profile.Value.Narrative.ClimateNotes);

        if (narrative.IsFailure)
        {
            return Result.Failure<NeighborhoodProfileResponse>(narrative.Error);
        }

        var updated = profile.Value.UpdateNarrative(
            narrative.Value, caller, now, request.BaseRevision);

        if (updated.IsFailure)
        {
            return Result.Failure<NeighborhoodProfileResponse>(updated.Error);
        }

        var index = profile.Value.UpdateLivingIndex(
            BuildIndex(profile.Value, request), caller, now);

        if (index.IsFailure)
        {
            return Result.Failure<NeighborhoodProfileResponse>(index.Error);
        }

        if (request.PopularLanguages is { Count: > 0 })
        {
            var languages = request.PopularLanguages
                .Select(language => NeighborhoodLanguage.Create(
                    language.Name,
                    Parse<NeighborhoodRating>(language.Prominence)
                        ?? NeighborhoodRating.Moderate))
                .ToList();

            var applied = profile.Value.SetPopularLanguages(languages, caller, now);

            if (applied.IsFailure)
            {
                return Result.Failure<NeighborhoodProfileResponse>(applied.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var statistics = await _statistics.ForAsync(profile.Value, cancellationToken);

        return Result.Success(NeighborhoodMapper.ToResponse(
            profile.Value,
            statistics,
            [],
            now,
            _options.Value.NeighborhoodReviewIntervalDays));
    }

    /// <summary>
    /// Builds the liveability index from whatever the caller supplied, keeping the rest.
    /// <para>
    /// Each band falls back to the stored value rather than to Unknown, so an editor correcting the
    /// noise level does not silently erase somebody else's walkability assessment.
    /// </para>
    /// </summary>
    private static NeighborhoodLivingIndex BuildIndex(
        NeighborhoodProfile profile,
        Contracts.V1.Requests.UpsertNeighborhoodProfileRequest request) =>
        NeighborhoodLivingIndex.Create(
            Parse<NeighborhoodRating>(request.Walkability)
                ?? profile.LivingIndex.Walkability,
            Parse<NeighborhoodRating>(request.FamilyFriendliness)
                ?? profile.LivingIndex.FamilyFriendliness,
            Parse<NeighborhoodRating>(request.StudentFriendliness)
                ?? profile.LivingIndex.StudentFriendliness,
            Parse<NeighborhoodRating>(request.Nightlife)
                ?? profile.LivingIndex.Nightlife,
            Parse<NeighborhoodRating>(request.NoiseLevel)
                ?? profile.LivingIndex.NoiseLevel,
            Parse<NeighborhoodRating>(request.Accessibility)
                ?? profile.LivingIndex.Accessibility,
            Parse<NeighborhoodRating>(request.ParkingAvailability)
                ?? profile.LivingIndex.ParkingAvailability,
            Parse<NeighborhoodRating>(request.CostOfLiving)
                ?? profile.LivingIndex.CostOfLiving,
            Parse<InternetAvailability>(request.InternetAvailability)
                ?? profile.LivingIndex.InternetAvailability);

    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;
}
