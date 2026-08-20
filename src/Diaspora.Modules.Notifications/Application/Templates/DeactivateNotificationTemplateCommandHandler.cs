using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>Retires a template.</summary>
public sealed class DeactivateNotificationTemplateCommandHandler
    : ICommandHandler<DeactivateNotificationTemplateCommand, NotificationTemplateResponse>
{
    private readonly INotificationTemplateRepository _templates;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public DeactivateNotificationTemplateCommandHandler(
        INotificationTemplateRepository templates,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _templates = templates;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<NotificationTemplateResponse>> Handle(
        DeactivateNotificationTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<NotificationTemplateResponse>(staff.Error);
        }

        var template = await _templates.GetAsync(
            NotificationTemplateId.From(command.TemplateId), cancellationToken);

        if (template is null)
        {
            return Result.Failure<NotificationTemplateResponse>(NotificationErrors.TemplateNotFound);
        }

        var deactivated = template.Deactivate(_clock.UtcNow);

        if (deactivated.IsFailure)
        {
            return Result.Failure<NotificationTemplateResponse>(deactivated.Error);
        }

        _templates.Update(template);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TemplateMapper.ToResponse(template));
    }
}
