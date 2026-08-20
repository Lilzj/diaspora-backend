using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Saved;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Application.Saved;

/// <summary>
/// Saves a listing, or updates the note and price-watch preference on one already saved.
/// <para>
/// Saving something the caller cannot see resolves as not-found rather than forbidden, for the same
/// reason every other read does: a 403 on a hidden listing confirms it exists.
/// </para>
/// </summary>
public sealed class SaveListingCommandHandler : ICommandHandler<SaveListingCommand>
{
    private readonly ISavedListingRepository _saved;
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SaveListingCommandHandler(
        ISavedListingRepository saved,
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IOptions<MarketplaceOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _saved = saved;
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result> Handle(
        SaveListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var listingId = MarketplaceListingId.From(command.ListingId);
        var listing = await _listings.GetForReadAsync(listingId, cancellationToken);

        if (listing is null || !await _authorization.CanSeeAsync(listing, cancellationToken))
        {
            return Result.Failure(MarketplaceErrors.ListingNotFound);
        }

        var existing = await _saved.FindAsync(caller.Value, listingId, cancellationToken);

        if (existing is not null)
        {
            // Already watching: this is an update to the note or the price-watch flag, not a second
            // save. The listing's count is untouched, which is what keeps it honest.
            existing.Update(
                command.Request?.Note, command.Request?.NotifyOnPriceChange ?? false);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        var held = await _saved.CountForUserAsync(caller.Value, cancellationToken);

        if (held >= _options.Value.MaxSavedListingsPerMember)
        {
            return Result.Failure(MarketplaceErrors.SavedListingLimit);
        }

        var saved = SavedListing.Create(
            SavedListingId.From(_guids.NewSequentialGuid()),
            caller.Value,
            listingId,
            _clock.UtcNow,
            command.Request?.Note,
            command.Request?.NotifyOnPriceChange ?? false);

        if (saved.IsFailure)
        {
            return Result.Failure(saved.Error);
        }

        await _saved.AddAsync(saved.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
