using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Preferences;

/// <summary>
/// The caller's email subscription state, per category — B13.3.
/// <para>
/// <b>A separate read from the preferences record, because it answers a different question.</b>
/// <c>GET /preferences</c> says what the platform will do next; this says what the member asked for,
/// when, and through which route. A settings screen needs the first. A member asking "why did I stop
/// getting these" — and anybody answering a complaint about mail that was or was not sent — needs the
/// second, and it is not derivable from a boolean.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.PreferencesRead)]
public sealed record ListNotificationSubscriptionsQuery
    : IQuery<IReadOnlyList<NotificationSubscriptionResponse>>;

/// <summary>
/// Reads every configurable category and reports where the caller stands on each.
/// <para>
/// <b>Driven by the catalogue rather than by the preference record.</b> A member who has never
/// touched a setting has no stored row for a category, and a list built from stored rows would show
/// them nothing at all — which reads as "you are subscribed to nothing" when the truth is the
/// opposite. Every category the platform has is listed, with the member's answer where they gave one
/// and the default where they did not.
/// </para>
/// </summary>
public sealed class ListNotificationSubscriptionsQueryHandler
    : IQueryHandler<ListNotificationSubscriptionsQuery, IReadOnlyList<NotificationSubscriptionResponse>>
{
    private readonly INotificationPreferenceRepository _preferences;
    private readonly INotificationCategoryRepository _categories;
    private readonly NotificationAuthorizationService _authorization;

    public ListNotificationSubscriptionsQueryHandler(
        INotificationPreferenceRepository preferences,
        INotificationCategoryRepository categories,
        NotificationAuthorizationService authorization)
    {
        _preferences = preferences;
        _categories = categories;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<NotificationSubscriptionResponse>>> Handle(
        ListNotificationSubscriptionsQuery query, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NotificationSubscriptionResponse>>(caller.Error);
        }

        var categories = await _categories.ListActiveAsync(cancellationToken);
        var preference = await _preferences.FindAsync(caller.Value, cancellationToken);

        var rows = categories
            .OrderBy(category => category.Position)
            .Select(category =>
            {
                var stored = preference?.Categories.FirstOrDefault(row =>
                    string.Equals(row.CategoryKey, category.Key, StringComparison.Ordinal));

                return new NotificationSubscriptionResponse
                {
                    CategoryKey = category.Key,
                    DisplayName = category.DisplayName,
                    IsMandatory = category.IsMandatory,

                    // A mandatory category is reported as subscribed whatever is stored. It is: the
                    // platform will send it, and showing "off" beside a switch that does nothing is
                    // how a member concludes the switch is broken.
                    EmailEnabled = category.IsMandatory || (stored?.Email ?? false),
                    UnsubscribedAt = category.IsMandatory ? null : stored?.UnsubscribedAt,
                    UnsubscribeSource = category.IsMandatory ? null : stored?.UnsubscribeSource
                };
            })
            .ToList();

        return Result.Success<IReadOnlyList<NotificationSubscriptionResponse>>(rows);
    }
}
