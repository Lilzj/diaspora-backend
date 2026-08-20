using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>The template catalogue.</summary>
public sealed class ListNotificationTemplatesQueryHandler
    : IQueryHandler<ListNotificationTemplatesQuery, CursorPagedResult<NotificationTemplateResponse>>
{
    private readonly INotificationTemplateRepository _templates;
    private readonly NotificationAuthorizationService _authorization;

    public ListNotificationTemplatesQueryHandler(
        INotificationTemplateRepository templates, NotificationAuthorizationService authorization)
    {
        _templates = templates;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<NotificationTemplateResponse>>> Handle(
        ListNotificationTemplatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<CursorPagedResult<NotificationTemplateResponse>>(staff.Error);
        }

        var page = await _templates.SearchAsync(
            new TemplateQueryFilter
            {
                Search = query.Search,
                CategoryKey = query.CategoryKey,
                Status = NotificationMapper.ParseEnum<TemplateStatus>(query.Status),
                Language = query.Language,
                Cursor = query.Cursor,
                Limit = query.Limit
            },
            cancellationToken);

        return Result.Success(CursorPagedResult<NotificationTemplateResponse>.Create(
            page.Items.Select(TemplateMapper.ToResponse).ToList(),
            page.NextCursor,
            page.HasMore));
    }
}
