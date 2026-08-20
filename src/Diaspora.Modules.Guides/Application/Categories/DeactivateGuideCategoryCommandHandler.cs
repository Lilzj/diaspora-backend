using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Categories;

/// <summary>Takes a category out of the picker without touching the guides filed under it.</summary>
public sealed class DeactivateGuideCategoryCommandHandler
    : ICommandHandler<DeactivateGuideCategoryCommand, GuideCategoryResponse>
{
    private readonly GuideCategoryResolver _categories;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateGuideCategoryCommandHandler(
        GuideCategoryResolver categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GuideCategoryResponse>> Handle(
        DeactivateGuideCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.FindAsync(command.Key, cancellationToken);

        if (category is null)
        {
            return Result.Failure<GuideCategoryResponse>(GuideErrors.CategoryNotFound);
        }

        var deactivated = category.Deactivate();

        if (deactivated.IsFailure)
        {
            return Result.Failure<GuideCategoryResponse>(deactivated.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideCategoryMapper.ToResponse(category));
    }
}
