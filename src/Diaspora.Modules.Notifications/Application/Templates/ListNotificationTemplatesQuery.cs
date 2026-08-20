using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>The template catalogue, for the staff authoring surface.</summary>
[Authorize(Permission = NotificationPermissions.TemplatesRead)]
public sealed record ListNotificationTemplatesQuery
    : IQuery<CursorPagedResult<NotificationTemplateResponse>>
{
    public string? Search { get; init; }

    public string? CategoryKey { get; init; }

    /// <summary><c>Draft</c>, <c>Published</c> or <c>Deactivated</c>.</summary>
    public string? Status { get; init; }

    public string? Language { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
