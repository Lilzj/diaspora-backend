using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>
/// Every version under one key.
/// <para>
/// The whole history rather than the live one, because the authoring screen's most useful panel is
/// "what did this used to say" — which is also the panel somebody needs during an incident caused by
/// a wording change.
/// </para>
/// </summary>
public sealed class GetNotificationTemplateQueryHandler
    : IQueryHandler<GetNotificationTemplateQuery, IReadOnlyList<NotificationTemplateResponse>>
{
    private readonly INotificationTemplateRepository _templates;
    private readonly NotificationAuthorizationService _authorization;

    public GetNotificationTemplateQueryHandler(
        INotificationTemplateRepository templates, NotificationAuthorizationService authorization)
    {
        _templates = templates;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<NotificationTemplateResponse>>> Handle(
        GetNotificationTemplateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NotificationTemplateResponse>>(staff.Error);
        }

        var versions = await _templates.ListVersionsAsync(query.Key, cancellationToken);

        if (versions.Count == 0)
        {
            return Result.Failure<IReadOnlyList<NotificationTemplateResponse>>(
                NotificationErrors.TemplateNotFound);
        }

        return Result.Success<IReadOnlyList<NotificationTemplateResponse>>(
            versions.Select(TemplateMapper.ToResponse).ToList());
    }
}
