using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Catalog;

/// <summary>
/// The category catalogue.
/// <para>
/// Readable by any signed-in member, because it is what a settings screen renders — and a client
/// that could not read it would have to hard-code the categories, which is precisely what making
/// them configurable was for.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.PreferencesRead)]
public sealed record ListNotificationCategoriesQuery
    : IQuery<IReadOnlyList<NotificationCategoryResponse>>;
