using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Localization;

/// <summary>
/// Refiles a guide, having first confirmed the target category is real and still open.
/// <para>
/// Inactive is a distinct refusal from unknown, because the fix differs: an unknown key is a
/// client bug, a retired one is a catalogue decision the author can be told about.
/// </para>
/// </summary>
public sealed class SetGuideCategoryCommandHandler
    : ICommandHandler<SetGuideCategoryCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideCategoryResolver _categories;

    public SetGuideCategoryCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        GuideCategoryResolver categories)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _categories = categories;
    }

    public async Task<Result<GuideResponse>> Handle(
        SetGuideCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guide = await _guides.GetAsync(GuideId.From(command.GuideId), cancellationToken);

        if (guide is null)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var caller = _authorization.RequireAuthor(guide);

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        var current = guide.EnsureRevision(command.Request.BaseRevision);

        if (current.IsFailure)
        {
            return Result.Failure<GuideResponse>(current.Error);
        }

        var category = await _categories.ResolveActiveAsync(
            command.Request.CategoryKey, cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<GuideResponse>(category.Error);
        }

        var changed = guide.ChangeCategory(category.Value.Key, baseRevision: null);

        if (changed.IsFailure)
        {
            return Result.Failure<GuideResponse>(changed.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideMapper.ToResponse(guide, forAuthor: true));
    }
}
