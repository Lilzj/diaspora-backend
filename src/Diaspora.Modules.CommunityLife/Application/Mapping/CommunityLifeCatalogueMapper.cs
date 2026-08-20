using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.Reports;

namespace Diaspora.Modules.CommunityLife.Application.Mapping;

/// <summary>Turns the catalogues, the reports and the measured records into wire shapes.</summary>
public static class CommunityLifeCatalogueMapper
{
    public static OrganizationTypeResponse ToResponse(OrganizationType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return new OrganizationTypeResponse
        {
            Key = type.Key,
            Name = type.Name,
            Description = type.Description,
            IconName = type.IconName,
            GroupName = type.GroupName,
            Order = type.Order,
            EncouragesVerification = type.EncouragesVerification,
            OrganizationCount = type.OrganizationCount
        };
    }

    public static EventCategoryResponse ToResponse(EventCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new EventCategoryResponse
        {
            Key = category.Key,
            Name = category.Name,
            Description = category.Description,
            IconName = category.IconName,
            GroupName = category.GroupName,
            Facets = CommunityLifeSharedMapper.SplitFacets(category.Facets),
            Order = category.Order,
            DefaultsToRsvp = category.DefaultsToRsvp,
            EventCount = category.EventCount
        };
    }

    /// <summary>
    /// A report.
    /// <para>
    /// <paramref name="includeReporter"/> is false everywhere except the moderator's own queue. A
    /// report is filed by somebody who may be in the same congregation as the person they reported,
    /// and naming them anywhere else would make reporting unsafe.
    /// </para>
    /// </summary>
    public static ReportResponse ToResponse(
        CommunityLifeReport report, bool includeReporter = false)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ReportResponse
        {
            ReportId = report.Id.Value,
            SubjectType = report.SubjectType.ToString(),
            SubjectId = report.SubjectId,
            Reason = report.Reason.ToString(),
            Detail = report.Detail,
            Status = report.Status.ToString(),
            ResolutionNote = report.ResolutionNote,
            MergedIntoSubjectId = report.MergedIntoSubjectId,
            ReportedByUserId = includeReporter ? report.ReportedByUserId.Value : null,
            CreatedAt = report.CreatedAt,
            ResolvedAt = report.ResolvedAt
        };
    }

    public static EventAnalyticsResponse ToResponse(EventAnalytics analytics)
    {
        ArgumentNullException.ThrowIfNull(analytics);

        return new EventAnalyticsResponse
        {
            EventId = analytics.EventId.Value,
            ViewCount = analytics.ViewCount,
            UniqueViewCount = analytics.UniqueViewCount,
            SaveCount = analytics.SaveCount,
            ShareCount = analytics.ShareCount,
            RsvpCount = analytics.RsvpCount,
            InterestedCount = analytics.InterestedCount,
            GoingCount = analytics.GoingCount,
            WaitlistedCount = analytics.WaitlistedCount,
            CancelledCount = analytics.CancelledCount,
            CheckedInCount = analytics.CheckedInCount,
            AttendedCount = analytics.AttendedCount,
            NoShowCount = analytics.NoShowCount,
            ConversionRate = analytics.ConversionRate,
            NoShowRate = analytics.NoShowRate,
            AttendanceRate = analytics.AttendanceRate,
            TimeToFillHours = analytics.TimeToFill?.TotalHours,
            PublishedAt = analytics.PublishedAt,
            CompletedAt = analytics.CompletedAt
        };
    }

    public static OrganizationAnalyticsResponse ToResponse(OrganizationAnalytics analytics)
    {
        ArgumentNullException.ThrowIfNull(analytics);

        return new OrganizationAnalyticsResponse
        {
            OrganizationId = analytics.OrganizationId.Value,
            ViewCount = analytics.ViewCount,
            UniqueViewCount = analytics.UniqueViewCount,
            TotalJoins = analytics.TotalJoins,
            TotalDepartures = analytics.TotalDepartures,
            TotalRemovals = analytics.TotalRemovals,
            CurrentMemberCount = analytics.CurrentMemberCount,
            PeakMemberCount = analytics.PeakMemberCount,
            EventsPublished = analytics.EventsPublished,
            EventsCompleted = analytics.EventsCompleted,
            EventsCancelled = analytics.EventsCancelled,
            TotalRsvps = analytics.TotalRsvps,
            TotalAttendances = analytics.TotalAttendances,
            TotalNoShows = analytics.TotalNoShows,
            VolunteerAttendances = analytics.VolunteerAttendances,
            RetentionRate = analytics.RetentionRate,
            AttendanceRate = analytics.AttendanceRate
        };
    }

    public static CategoryPerformanceResponse ToResponse(
        CategoryPerformance performance, string? categoryName = null)
    {
        ArgumentNullException.ThrowIfNull(performance);

        return new CategoryPerformanceResponse
        {
            CategoryKey = performance.CategoryKey,
            CategoryName = categoryName,
            EventCount = performance.EventCount,
            PublishedCount = performance.PublishedCount,
            CompletedCount = performance.CompletedCount,
            TotalRsvps = performance.TotalRsvps,
            TotalAttendances = performance.TotalAttendances,
            TotalNoShows = performance.TotalNoShows,
            TotalViews = performance.TotalViews,
            AttendanceRate = performance.AttendanceRate,
            ConversionRate = performance.ConversionRate
        };
    }
}
