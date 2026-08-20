using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Inboxes;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Extensibility.Search;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member's own inbox.
/// <para>
/// <b>Routed through the search engine seam even when nothing is being searched.</b> One code path
/// for the list and the search means a future OpenSearch adapter serves both, and means the two
/// cannot start disagreeing about what "archived" includes.
/// </para>
/// <para>
/// The unread count rides along from the maintained counter. The badge and the first page are always
/// fetched together, and a second round trip for a number the server already has is a round trip on
/// every app launch.
/// </para>
/// </summary>
public sealed class GetInboxQueryHandler : IQueryHandler<GetInboxQuery, NotificationPageResponse>
{
    private readonly INotificationSearchEngine _search;
    private readonly INotificationCategoryRepository _categories;
    private readonly IUserInboxRepository _inboxes;
    private readonly NotificationAuthorizationService _authorization;

    public GetInboxQueryHandler(
        INotificationSearchEngine search,
        INotificationCategoryRepository categories,
        IUserInboxRepository inboxes,
        NotificationAuthorizationService authorization)
    {
        _search = search;
        _categories = categories;
        _inboxes = inboxes;
        _authorization = authorization;
    }

    public async Task<Result<NotificationPageResponse>> Handle(
        GetInboxQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NotificationPageResponse>(caller.Error);
        }

        var filter = new NotificationQueryFilter
        {
            RecipientUserId = caller.Value,
            Scope = string.IsNullOrWhiteSpace(query.Scope) ? "inbox" : query.Scope.Trim().ToLowerInvariant(),
            UnreadOnly = query.UnreadOnly,
            CategoryKeys = query.Categories,
            Priorities = NotificationMapper.ParseEnums<NotificationPriority>(query.Priorities),
            Search = query.Search,
            SourceModule = query.SourceModule,
            EntityType = query.EntityType,
            CreatedAfter = query.From,
            CreatedBefore = query.To,
            Cursor = query.Cursor,
            Limit = query.Limit
        };

        var page = await _search.SearchAsync(filter, cancellationToken);
        var categories = await _categories.ListActiveAsync(cancellationToken);
        var lookup = categories.ToDictionary(category => category.Key, StringComparer.Ordinal);
        var inbox = await _inboxes.FindAsync(caller.Value, cancellationToken);

        return Result.Success(new NotificationPageResponse
        {
            Items = page.Items
                .Select(notification => NotificationMapper.ToResponse(notification, lookup))
                .ToList(),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            UnreadCount = inbox?.UnreadCount ?? 0
        });
    }
}
