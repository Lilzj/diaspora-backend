using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Categories;

/// <summary>Applies an edit to a catalogue entry, resolved through the request-scoped cache.</summary>
public sealed class UpdateGuideCategoryCommandHandler
    : ICommandHandler<UpdateGuideCategoryCommand, GuideCategoryResponse>
{
    private readonly GuideCategoryResolver _categories;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGuideCategoryCommandHandler(
        GuideCategoryResolver categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GuideCategoryResponse>> Handle(
        UpdateGuideCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.FindAsync(command.Key, cancellationToken);

        if (category is null)
        {
            return Result.Failure<GuideCategoryResponse>(GuideErrors.CategoryNotFound);
        }

        var request = command.Request;

        // A parent that does not exist would leave the catalogue with a branch hanging off
        // nothing, which every renderer of the tree has to special-case forever.
        if (request.ParentKey is not null
            && await _categories.FindAsync(request.ParentKey, cancellationToken) is null)
        {
            return Result.Failure<GuideCategoryResponse>(GuideErrors.CategoryNotFound);
        }

        var updated = category.Update(
            request.Name, request.Description, request.Icon, request.Order, request.ParentKey);

        if (updated.IsFailure)
        {
            return Result.Failure<GuideCategoryResponse>(updated.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideCategoryMapper.ToResponse(category));
    }
}
