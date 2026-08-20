using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>
/// Creates a draft template.
/// <para>
/// A key that already has a draft is refused rather than merged: two people authoring the same
/// template at once is a conversation to have, not a conflict to resolve silently.
/// </para>
/// </summary>
public sealed class CreateNotificationTemplateCommandHandler
    : ICommandHandler<CreateNotificationTemplateCommand, NotificationTemplateResponse>
{
    private readonly INotificationTemplateRepository _templates;
    private readonly INotificationCategoryRepository _categories;
    private readonly TemplateAssembler _assembler;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateNotificationTemplateCommandHandler(
        INotificationTemplateRepository templates,
        INotificationCategoryRepository categories,
        TemplateAssembler assembler,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _templates = templates;
        _categories = categories;
        _assembler = assembler;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<NotificationTemplateResponse>> Handle(
        CreateNotificationTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<NotificationTemplateResponse>(staff.Error);
        }

        var request = command.Request;

        if (await _categories.FindByKeyAsync(request.CategoryKey, cancellationToken) is null)
        {
            return Result.Failure<NotificationTemplateResponse>(NotificationErrors.CategoryNotFound);
        }

        if (await _templates.FindDraftAsync(request.Key, cancellationToken) is not null)
        {
            return Result.Failure<NotificationTemplateResponse>(NotificationErrors.TemplateKeyTaken);
        }

        var version = await _templates.HighestVersionAsync(request.Key, cancellationToken) + 1;

        var draft = NotificationTemplate.Draft(
            NotificationTemplateId.From(_guids.NewSequentialGuid()),
            request.Key,
            request.CategoryKey,
            NotificationMapper.ParseEnum<NotificationPriority>(request.Priority)
                ?? NotificationPriority.Normal,
            _clock.UtcNow,
            version,
            request.DefaultLanguage,
            request.Description,
            request.RouteTemplate,
            request.RetentionDays is { } days ? TimeSpan.FromDays(days) : null);

        if (draft.IsFailure)
        {
            return Result.Failure<NotificationTemplateResponse>(draft.Error);
        }

        var applied = _assembler.Apply(
            draft.Value,
            request.Localizations,
            request.Variables,
            request.Variants,
            request.Actions);

        if (applied.IsFailure)
        {
            return Result.Failure<NotificationTemplateResponse>(applied.Error);
        }

        await _templates.AddAsync(draft.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TemplateMapper.ToResponse(draft.Value));
    }
}
