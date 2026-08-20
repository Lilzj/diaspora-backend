using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Analytics;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Analytics;

/// <summary>
/// The measured record.
/// <para>
/// <b>Every rate is computed from summed counters, never averaged from daily rates.</b> A week's
/// open rate is total opens over total deliveries; the mean of seven daily percentages is a
/// different number, and it is wrong whenever the days had different volumes — which is every week.
/// </para>
/// <para>
/// Three breakdowns come back with one query's worth of rows because the grain already carries all
/// three dimensions. Asking the database three times would be three scans of the same range.
/// </para>
/// </summary>
public sealed class GetNotificationAnalyticsQueryHandler
    : IQueryHandler<GetNotificationAnalyticsQuery, NotificationAnalyticsResponse>
{
    /// <summary>Matches the repository's cap, so truncation can be reported honestly.</summary>
    private const int RowLimit = 5000;

    private readonly INotificationAnalyticsRepository _analytics;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IClock _clock;

    public GetNotificationAnalyticsQueryHandler(
        INotificationAnalyticsRepository analytics,
        NotificationAuthorizationService authorization,
        IClock clock)
    {
        _analytics = analytics;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<NotificationAnalyticsResponse>> Handle(
        GetNotificationAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<NotificationAnalyticsResponse>(staff.Error);
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var from = query.From ?? today.AddDays(-29);
        var to = query.To ?? today;

        var rows = await _analytics.QueryAsync(
            new NotificationAnalyticsFilter
            {
                From = from,
                To = to,
                CategoryKey = query.CategoryKey,
                Channel = NotificationMapper.ParseEnum<NotificationChannelKind>(query.Channel),
                TemplateKey = query.TemplateKey,
                Limit = RowLimit
            },
            cancellationToken);

        return Result.Success(NotificationAnalyticsAggregator.Aggregate(rows, from, to, RowLimit));
    }
}
