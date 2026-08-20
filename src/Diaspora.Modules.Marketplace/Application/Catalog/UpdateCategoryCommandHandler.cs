using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Catalog;

/// <summary>
/// Renames or retires a node.
/// <para>
/// <b>Retiring a node with live children is refused.</b> A parent that is closed for new listings
/// while its subcategories are open leaves a create form that cannot be navigated to — so the
/// subtree is retired from the leaves up, deliberately, by whoever decided to.
/// </para>
/// </summary>
public sealed class UpdateCategoryCommandHandler
    : ICommandHandler<UpdateCategoryCommand, ListingCategoryResponse>
{
    private readonly IListingCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCategoryCommandHandler(
        IListingCategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ListingCategoryResponse>> Handle(
        UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.GetByKeyAsync(command.Key, cancellationToken);

        if (category is null)
        {
            return Result.Failure<ListingCategoryResponse>(MarketplaceErrors.CategoryNotFound);
        }

        var updated = category.Update(
            command.Request.Name,
            command.Request.Description,
            command.Request.IconKey,
            command.Request.Order);

        if (updated.IsFailure)
        {
            return Result.Failure<ListingCategoryResponse>(updated.Error);
        }

        if (command.Request.IsActive)
        {
            category.Reinstate();
        }
        else if (category.IsActive)
        {
            if (await _categories.HasChildrenAsync(category.Key, cancellationToken))
            {
                return Result.Failure<ListingCategoryResponse>(
                    MarketplaceErrors.CategoryHasChildren);
            }

            category.Retire();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CatalogMapper.ToResponse(category));
    }
}
