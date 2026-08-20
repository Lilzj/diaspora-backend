using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Application.Preferences;

/// <summary>
/// The settings screen.
/// <para>
/// A member with no preference row gets the platform defaults rendered against the full category
/// catalogue — which is what makes "created on demand" invisible to the client. Reading the screen
/// writes nothing.
/// </para>
/// </summary>
public sealed class GetNotificationPreferencesQueryHandler
    : IQueryHandler<GetNotificationPreferencesQuery, NotificationPreferencesResponse>
{
    private readonly INotificationPreferenceRepository _preferences;
    private readonly INotificationCategoryRepository _categories;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IOptions<NotificationOptions> _options;

    public GetNotificationPreferencesQueryHandler(
        INotificationPreferenceRepository preferences,
        INotificationCategoryRepository categories,
        NotificationAuthorizationService authorization,
        IOptions<NotificationOptions> options)
    {
        _preferences = preferences;
        _categories = categories;
        _authorization = authorization;
        _options = options;
    }

    public async Task<Result<NotificationPreferencesResponse>> Handle(
        GetNotificationPreferencesQuery query, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NotificationPreferencesResponse>(caller.Error);
        }

        var preference = await _preferences.FindAsync(caller.Value, cancellationToken);
        var categories = await _categories.ListActiveAsync(cancellationToken);

        return Result.Success(PreferenceMapper.ToResponse(
            preference, categories, _options.Value.DefaultLanguage, _options.Value.DefaultTimeZoneId));
    }
}
