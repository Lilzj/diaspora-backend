using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Integration;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Application.Mapping;

/// <summary>
/// Turns neighbourhood profiles and insights into wire responses, and into the published summary
/// other modules consume.
/// <para>
/// <b>Two output shapes from one aggregate, and the difference is ownership.</b>
/// <see cref="ToResponse"/> produces Housing's own contract, which may change with Housing.
/// <see cref="ToSummary"/> produces <see cref="NeighborhoodSummary"/>, which is the published
/// contract other modules bind to and which must not change with Housing. Keeping both in one file
/// makes it obvious when a change to the first is being carelessly copied into the second.
/// </para>
/// </summary>
public static class NeighborhoodMapper
{
    public static NeighborhoodProfileResponse ToResponse(
        NeighborhoodProfile profile,
        NeighborhoodStatisticsResponse statistics,
        IReadOnlyList<NeighborhoodInsightResponse> insights,
        DateTimeOffset now,
        int reviewIntervalDays)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(insights);

        return new NeighborhoodProfileResponse
        {
            AreaId = profile.AreaId,
            Name = profile.Name,
            Slug = profile.Slug,
            CountryCode = profile.CountryCode,
            CityName = profile.CityName,
            RegionName = profile.RegionName,

            Description = profile.Narrative.Description,
            Overview = profile.Narrative.Overview,
            SafetySummary = profile.Narrative.SafetySummary,
            TransportSummary = profile.Narrative.TransportSummary,
            CostOfLivingSummary = profile.Narrative.CostOfLivingSummary,
            CommunitySummary = profile.Narrative.CommunitySummary,
            ClimateNotes = profile.Narrative.ClimateNotes,

            LivingIndex = ToLivingIndex(profile),
            PopularLanguages = [.. profile.PopularLanguages.Select(language =>
                new NeighborhoodLanguageResponse
                {
                    Name = language.Name,
                    Prominence = language.Prominence.ToString()
                })],
            Insights = insights,
            Statistics = statistics,

            IsApproved = profile.IsApproved,
            Completeness = profile.Completeness,
            LastReviewedAt = profile.LastReviewedAt,
            NeedsReview = NeedsReview(profile, now, reviewIntervalDays),
            Revision = profile.Revision
        };
    }

    public static NeighborhoodSummaryResponse ToSummaryResponse(NeighborhoodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new NeighborhoodSummaryResponse
        {
            AreaId = profile.AreaId,
            Name = profile.Name,
            Slug = profile.Slug,
            CountryCode = profile.CountryCode,
            CityName = profile.CityName,
            Description = profile.Narrative.Description,
            ListingCount = profile.ListingCount,
            InsightCount = profile.ApprovedInsightCount,
            IsApproved = profile.IsApproved,
            Completeness = profile.Completeness,
            PopularLanguages = [.. profile.PopularLanguages.Select(language => language.Name)]
        };
    }

    /// <summary>
    /// The published contract other modules bind to.
    /// <para>
    /// <b>Bands are rendered as their names and <c>null</c> means unassessed.</b> A consumer must
    /// be able to tell "nobody has rated this" from "rated lowest", because the first should render
    /// as a gap and the second as a warning.
    /// </para>
    /// </summary>
    public static NeighborhoodSummary ToSummary(NeighborhoodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new NeighborhoodSummary
        {
            AreaId = profile.AreaId,
            Name = profile.Name,
            Slug = profile.Slug,
            CountryCode = profile.CountryCode,
            CityName = profile.CityName,
            RegionName = profile.RegionName,
            Description = profile.Narrative.Description,
            Walkability = Band(profile.LivingIndex.Walkability),
            SafetyBand = null,
            CostOfLivingBand = Band(profile.LivingIndex.CostOfLiving),
            NoiseLevel = Band(profile.LivingIndex.NoiseLevel),
            FamilyFriendliness = Band(profile.LivingIndex.FamilyFriendliness),
            StudentFriendliness = Band(profile.LivingIndex.StudentFriendliness),
            InternetAvailability =
                profile.LivingIndex.InternetAvailability == Domain.Enums.InternetAvailability.Unknown
                    ? null
                    : profile.LivingIndex.InternetAvailability.ToString(),
            PopularLanguages = [.. profile.PopularLanguages.Select(language => language.Name)],
            InsightCount = profile.ApprovedInsightCount,
            ListingCount = profile.ListingCount,
            IsApproved = profile.IsApproved,
            Completeness = profile.Completeness,
            LastReviewedAt = profile.LastReviewedAt
        };
    }

    public static NeighborhoodLivingIndexResponse ToLivingIndex(NeighborhoodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new NeighborhoodLivingIndexResponse
        {
            Walkability = profile.LivingIndex.Walkability.ToString(),
            FamilyFriendliness = profile.LivingIndex.FamilyFriendliness.ToString(),
            StudentFriendliness = profile.LivingIndex.StudentFriendliness.ToString(),
            Nightlife = profile.LivingIndex.Nightlife.ToString(),
            NoiseLevel = profile.LivingIndex.NoiseLevel.ToString(),
            Accessibility = profile.LivingIndex.Accessibility.ToString(),
            ParkingAvailability = profile.LivingIndex.ParkingAvailability.ToString(),
            CostOfLiving = profile.LivingIndex.CostOfLiving.ToString(),
            InternetAvailability = profile.LivingIndex.InternetAvailability.ToString(),
            Assessed = profile.LivingIndex.Assessed
        };
    }

    public static NeighborhoodInsightResponse ToResponse(
        NeighborhoodInsight insight,
        UserId? viewerId,
        bool isStaff,
        bool hasEndorsed)
    {
        ArgumentNullException.ThrowIfNull(insight);

        var isAuthor = viewerId is not null && insight.AuthorId == viewerId;

        return new NeighborhoodInsightResponse
        {
            Id = insight.Id.Value,
            AreaId = insight.AreaId,
            Category = insight.Category.ToString(),
            Body = insight.Body,
            Label = insight.Label,
            Status = insight.Status.ToString(),
            EndorsementCount = insight.EndorsementCount,
            HasEndorsed = hasEndorsed,
            IsAuthor = isAuthor,

            // The moderator reason is written for the author, so the author reads it. Nobody else
            // needs to know why somebody else's observation was refused.
            DecisionNote = isAuthor || isStaff ? insight.DecisionNote : null,

            CreatedAt = insight.CreatedAt
        };
    }

    /// <summary>
    /// Whether a profile has gone past the deployment's review interval.
    /// <para>
    /// A profile that has never been reviewed but has content counts as needing review — an
    /// unreviewed description is exactly what the queue exists to find. An empty profile does not,
    /// because there is nothing to review.
    /// </para>
    /// </summary>
    public static bool NeedsReview(
        NeighborhoodProfile profile, DateTimeOffset now, int reviewIntervalDays)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.HasContent)
        {
            return false;
        }

        return profile.LastReviewedAt is not { } reviewed
            || reviewed < now.AddDays(-Math.Max(1, reviewIntervalDays));
    }

    private static string? Band(Domain.Enums.NeighborhoodRating rating) =>
        rating == Domain.Enums.NeighborhoodRating.Unknown ? null : rating.ToString();
}
