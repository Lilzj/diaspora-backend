using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Catalogue;

/// <summary>
/// Writes a new notice category.
/// <para>
/// The key check is advisory — the unique index is what actually guarantees uniqueness — but it turns
/// the ordinary case, an administrator retyping a key that already exists, into a clear conflict
/// rather than a constraint violation and a rolled-back transaction. It checks archived rows too,
/// because an archived key is still taken and re-creating it would strand the archived row beyond any
/// restore.
/// </para>
/// </summary>
public sealed class CreateNoticeCategoryCommandHandler
    : ICommandHandler<CreateNoticeCategoryCommand, NoticeCategoryResponse>
{
    private readonly INoticeCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateNoticeCategoryCommandHandler(
        INoticeCategoryRepository categories,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<NoticeCategoryResponse>> Handle(
        CreateNoticeCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (await _categories.KeyExistsAsync(request.Key, cancellationToken))
        {
            return Result.Failure<NoticeCategoryResponse>(IntelligenceErrors.CategoryKeyTaken);
        }

        var caller = _authorization.CallerId;

        var created = NoticeCategory.Create(
            NoticeCategoryId.From(_guids.NewSequentialGuid()),
            request.Key,
            request.Name,
            _clock.UtcNow,
            request.Description,
            request.Icon,
            request.Order,
            IntelligenceWireNames.ParseSeverity(request.DefaultSeverity)
                ?? NoticeSeverity.Informational,
            IntelligenceWireNames.ParseAlertFamily(request.AlertFamily) ?? NoticeAlertFamily.None,
            caller);

        if (created.IsFailure)
        {
            return Result.Failure<NoticeCategoryResponse>(created.Error);
        }

        var category = created.Value;

        if (request.Localizations.Count > 0)
        {
            var localized = category.SetLocalizations(
                [.. request.Localizations.Select(localization =>
                    ((string?)localization.Locale,
                        (string?)localization.Name,
                        localization.Description))],
                caller);

            if (localized.IsFailure)
            {
                return Result.Failure<NoticeCategoryResponse>(localized.Error);
            }
        }

        await _categories.AddAsync(category, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(IntelligenceCatalogueMapper.ToEditorResponse(category));
    }
}

/// <summary>Rewrites a category. The key never changes — it is the contract.</summary>
public sealed class UpdateNoticeCategoryCommandHandler
    : ICommandHandler<UpdateNoticeCategoryCommand, NoticeCategoryResponse>
{
    private readonly INoticeCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;

    public UpdateNoticeCategoryCommandHandler(
        INoticeCategoryRepository categories,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<NoticeCategoryResponse>> Handle(
        UpdateNoticeCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.GetByKeyAsync(command.Key, cancellationToken);

        if (category is null)
        {
            return Result.Failure<NoticeCategoryResponse>(IntelligenceErrors.CategoryNotFound);
        }

        var request = command.Request;

        var revision = category.EnsureRevision(request.ExpectedRevision);

        if (revision.IsFailure)
        {
            return Result.Failure<NoticeCategoryResponse>(revision.Error);
        }

        var caller = _authorization.CallerId;

        var updated = category.Update(
            request.Name,
            request.Description,
            request.Icon,
            request.Order,
            IntelligenceWireNames.ParseSeverity(request.DefaultSeverity)
                ?? category.DefaultSeverity,
            IntelligenceWireNames.ParseAlertFamily(request.AlertFamily) ?? category.AlertFamily,
            caller);

        if (updated.IsFailure)
        {
            return Result.Failure<NoticeCategoryResponse>(updated.Error);
        }

        if (request.Localizations is { } localizations)
        {
            var localized = category.SetLocalizations(
                [.. localizations.Select(localization =>
                    ((string?)localization.Locale,
                        (string?)localization.Name,
                        localization.Description))],
                caller);

            if (localized.IsFailure)
            {
                return Result.Failure<NoticeCategoryResponse>(localized.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(IntelligenceCatalogueMapper.ToEditorResponse(category));
    }
}

/// <summary>
/// Retires a category.
/// <para>
/// <b>The in-use check counts archived notices too.</b> A notice restored into a category that was
/// retired while it was hidden would be filed under something no listing shows and no editor can
/// find — a row that exists and is unreachable, which is worse than either state on its own.
/// </para>
/// </summary>
public sealed class ArchiveNoticeCategoryCommandHandler
    : ICommandHandler<ArchiveNoticeCategoryCommand>
{
    private readonly INoticeCategoryRepository _categories;
    private readonly IOfficialNoticeRepository _notices;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveNoticeCategoryCommandHandler(
        INoticeCategoryRepository categories,
        IOfficialNoticeRepository notices,
        IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _notices = notices;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ArchiveNoticeCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.GetByKeyAsync(command.Key, cancellationToken);

        if (category is null)
        {
            return Result.Failure(IntelligenceErrors.CategoryNotFound);
        }

        if (await _notices.AnyForCategoryAsync(category.Key, cancellationToken))
        {
            return Result.Failure(IntelligenceErrors.CategoryInUse);
        }

        _categories.Remove(category);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Returns an archived category to the catalogue.
/// <para>
/// Reads through <c>GetIncludingArchivedAsync</c>, because the global query filter hides exactly the
/// row a restore needs — the blind spot the platform recorded once and has paid for once.
/// </para>
/// </summary>
public sealed class RestoreNoticeCategoryCommandHandler
    : ICommandHandler<RestoreNoticeCategoryCommand, NoticeCategoryResponse>
{
    private readonly INoticeCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public RestoreNoticeCategoryCommandHandler(
        INoticeCategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<NoticeCategoryResponse>> Handle(
        RestoreNoticeCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.GetIncludingArchivedAsync(
            command.Key, cancellationToken);

        if (category is null)
        {
            return Result.Failure<NoticeCategoryResponse>(IntelligenceErrors.CategoryNotFound);
        }

        if (!category.IsDeleted)
        {
            return Result.Failure<NoticeCategoryResponse>(IntelligenceErrors.CategoryNotArchived);
        }

        category.Restore();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(IntelligenceCatalogueMapper.ToEditorResponse(category));
    }
}
