namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// The measured record over a window.
/// <para>
/// The rates are computed from the summed counters rather than averaged from daily rates, because
/// averages do not add: a week's open rate is total opens over total deliveries, not the mean of
/// seven daily percentages weighted by nothing.
/// </para>
/// </summary>
public sealed record NotificationAnalyticsResponse
{
    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }

    public required long Created { get; init; }

    public required long Queued { get; init; }

    public required long Delivered { get; init; }

    public required long Opened { get; init; }

    public required long Clicked { get; init; }

    public required long Dismissed { get; init; }

    public required long Archived { get; init; }

    public required long Failed { get; init; }

    public required long Suppressed { get; init; }

    public required long Digested { get; init; }

    public required long Retries { get; init; }

    public required double DeliveryRate { get; init; }

    public required double OpenRate { get; init; }

    public required double ClickThroughRate { get; init; }

    public required double DismissRate { get; init; }

    public required double FailureRate { get; init; }

    public required double AverageMinutesToOpen { get; init; }

    /// <summary>The same numbers split by channel. Where "push is broken" becomes visible.</summary>
    public required IReadOnlyList<NotificationAnalyticsBreakdownResponse> ByChannel { get; init; }

    /// <summary>The same numbers split by category.</summary>
    public required IReadOnlyList<NotificationAnalyticsBreakdownResponse> ByCategory { get; init; }

    /// <summary>The same numbers split by template. Where a bad wording becomes visible.</summary>
    public required IReadOnlyList<NotificationAnalyticsBreakdownResponse> ByTemplate { get; init; }

    /// <summary>True when the row cap was reached and the window shown is narrower than asked for.</summary>
    public required bool IsTruncated { get; init; }
}
