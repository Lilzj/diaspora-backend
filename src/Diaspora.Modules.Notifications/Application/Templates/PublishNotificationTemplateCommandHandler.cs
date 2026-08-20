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

/// <summary>
/// Makes a template usable, and retires whatever it replaces.
/// <para>
/// <b>The previous version is deactivated in the same transaction.</b> Two published versions under
/// one key would both be resolvable and the dispatcher would take the higher — correct, but for a
/// window in which an operator reading the catalogue cannot tell which one is live. Retiring
/// explicitly makes the answer visible in the data rather than derivable from a sort order.
/// </para>
/// </summary>
public sealed class PublishNotificationTemplateCommandHandler
    : ICommandHandler<PublishNotificationTemplateCommand, NotificationTemplateResponse>
{
    private readonly INotificationTemplateRepository _templates;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public PublishNotificationTemplateCommandHandler(
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
        PublishNotificationTemplateCommand command, CancellationToken cancellationToken)
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

        var now = _clock.UtcNow;
        var superseded = await _templates.FindPublishedAsync(template.Key, cancellationToken);

        var published = template.Publish(now);

        if (published.IsFailure)
        {
            return Result.Failure<NotificationTemplateResponse>(published.Error);
        }

        _templates.Update(template);

        if (superseded is not null && superseded.Id != template.Id)
        {
            superseded.Deactivate(now);
            _templates.Update(superseded);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TemplateMapper.ToResponse(template));
    }
}
