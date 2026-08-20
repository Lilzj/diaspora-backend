using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Catalog;

/// <summary>
/// Creates a category node under an existing parent, or at the top.
/// <para>
/// The parent's materialised path is read and extended here rather than computed from keys, because
/// the path is the thing every subtree query prefix-matches on — and a node whose path did not
/// actually descend from its parent's would be invisible to every one of them.
/// </para>
/// </summary>
public sealed class CreateCategoryCommandHandler
    : ICommandHandler<CreateCategoryCommand, ListingCategoryResponse>
{
    private readonly IListingCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateCategoryCommandHandler(
        IListingCategoryRepository categories,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ListingCategoryResponse>> Handle(
        CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        var normalized = ListingCategory.NormalizeKey(request.Key);

        if (normalized is null)
        {
            return Result.Failure<ListingCategoryResponse>(
                MarketplaceErrors.CategoryKeyInvalid);
        }

        if (await _categories.GetByKeyAsync(normalized, cancellationToken) is not null)
        {
            return Result.Failure<ListingCategoryResponse>(MarketplaceErrors.CategoryKeyTaken);
        }

        string? parentPath = null;

        if (!string.IsNullOrWhiteSpace(request.ParentKey))
        {
            var parent = await _categories.GetByKeyAsync(request.ParentKey, cancellationToken);

            if (parent is null)
            {
                return Result.Failure<ListingCategoryResponse>(
                    MarketplaceErrors.CategoryNotFound);
            }

            parentPath = parent.Path;
        }

        var category = ListingCategory.Create(
            ListingCategoryId.From(_guids.NewSequentialGuid()),
            normalized,
            request.Name,
            _clock.UtcNow,
            request.ParentKey,
            parentPath,
            request.Description,
            request.IconKey,
            request.Order);

        if (category.IsFailure)
        {
            return Result.Failure<ListingCategoryResponse>(category.Error);
        }

        await _categories.AddAsync(category.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CatalogMapper.ToResponse(category.Value));
    }
}
