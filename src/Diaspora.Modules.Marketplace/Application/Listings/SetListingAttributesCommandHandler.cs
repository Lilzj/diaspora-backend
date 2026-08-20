using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Validates a set of attributes against the category and replaces what the listing holds.
/// <para>
/// <b>Validated against the category's definitions, not against a fixed list.</b> An attribute with
/// no definition is accepted — a seller volunteering a fact the catalogue has not thought of is
/// adding information — and one a definition explicitly rejects is refused, which is the whole
/// value of typing them.
/// </para>
/// </summary>
public sealed class SetListingAttributesCommandHandler
    : ICommandHandler<SetListingAttributesCommand>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly ListingCategoryResolver _categories;
    private readonly ListingAttributeValidator _attributes;

    public SetListingAttributesCommandHandler(
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        ListingCategoryResolver categories,
        ListingAttributeValidator attributes)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _categories = categories;
        _attributes = attributes;
    }

    public async Task<Result> Handle(
        SetListingAttributesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _listings.GetAsync(
            MarketplaceListingId.From(command.ListingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure(MarketplaceErrors.ListingNotFound);
        }

        var seller = await _authorization.RequireListingAccessAsync(listing, cancellationToken);

        if (seller.IsFailure)
        {
            return Result.Failure(seller.Error);
        }

        var drafts = command.Request.Attributes
            .Select(attribute => new ListingAttributeDraft
            {
                Key = attribute.Key,
                Value = attribute.Value,
                DisplayName = attribute.DisplayName
            })
            .ToList();

        var category = await _categories.FindAsync(listing.CategoryKey, cancellationToken);

        if (category is not null)
        {
            var valid = await _attributes.ValidateAsync(
                category.Path, drafts, cancellationToken);

            if (valid.IsFailure)
            {
                return valid;
            }
        }

        var applied = listing.SetAttributes(drafts, command.Request.BaseRevision);

        if (applied.IsFailure)
        {
            return applied;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
