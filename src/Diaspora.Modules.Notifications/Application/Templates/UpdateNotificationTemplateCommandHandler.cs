using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>
/// Edits a draft, or opens the next version of a published template and edits that.
/// <para>
/// The caller does not choose which. Asking a template author to decide whether they are creating a
/// version is asking them to get it wrong once, on the template that matters.
/// </para>
/// </summary>
public sealed class UpdateNotificationTemplateCommandHandler
    : ICommandHandler<UpdateNotificationTemplateCommand, NotificationTemplateResponse>
{
    private readonly INotificationTemplateRepository _templates;
    private readonly TemplateAssembler _assembler;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public UpdateNotificationTemplateCommandHandler(
        INotificationTemplateRepository templates,
        TemplateAssembler assembler,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _templates = templates;
        _assembler = assembler;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<NotificationTemplateResponse>> Handle(
        UpdateNotificationTemplateCommand command, CancellationToken cancellationToken)
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

        var target = template;

        if (template.Status != TemplateStatus.Draft)
        {
            var next = template.CreateNextVersion(
                NotificationTemplateId.From(_guids.NewSequentialGuid()), _clock.UtcNow);

            if (next.IsFailure)
            {
                return Result.Failure<NotificationTemplateResponse>(next.Error);
            }

            target = next.Value;

            await _templates.AddAsync(target, cancellationToken);
        }

        var request = command.Request;

        var metadata = target.UpdateMetadata(
            request.Description ?? target.Description,
            request.RouteTemplate ?? target.RouteTemplate,
            NotificationMapper.ParseEnum<NotificationPriority>(request.Priority),
            request.RetentionDays is { } days ? TimeSpan.FromDays(days) : target.RetentionPeriod);

        if (metadata.IsFailure)
        {
            return Result.Failure<NotificationTemplateResponse>(metadata.Error);
        }

        var applied = _assembler.Apply(
            target, request.Localizations, request.Variables, request.Variants, request.Actions);

        if (applied.IsFailure)
        {
            return Result.Failure<NotificationTemplateResponse>(applied.Error);
        }

        _templates.Update(target);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TemplateMapper.ToResponse(target));
    }
}
