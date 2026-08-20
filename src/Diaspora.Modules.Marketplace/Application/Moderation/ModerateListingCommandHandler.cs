using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>
/// Applies a moderation decision.
/// <para>
/// <b>Hiding and suspending are different words to the seller, and that is why they are different
/// actions.</b> Hidden means "we are looking at this"; suspended means "we looked, and it stays
/// down". A single "take it down" would leave every seller guessing which had happened to them.
/// </para>
/// <para>
/// Every decision also appends to the editorial trail, which is never edited and never pruned.
/// </para>
/// </summary>
public sealed class ModerateListingCommandHandler : ICommandHandler<ModerateListingCommand>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ModerateListingCommandHandler(
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result> Handle(
        ModerateListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        if (!_authorization.IsStaff)
        {
            return Result.Failure(MarketplaceErrors.NotModerator);
        }

        // Staff work the queue on hidden and deleted listings by definition, so the read that
        // ignores the soft-delete filter is the right one here.
        var listing = await _listings.GetIncludingDeletedAsync(
            MarketplaceListingId.From(command.ListingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure(MarketplaceErrors.ListingNotFound);
        }

        var now = _clock.UtcNow;
        var reason = command.Request.Reason;

        var applied = command.Request.Action.Trim().ToLowerInvariant() switch
        {
            "flag" => listing.Flag(caller.Value, reason, now),
            "hide" => listing.Hide(reason, caller.Value, now),
            "suspend" => listing.Suspend(reason, caller.Value, now),
            "restore" => listing.Unhide(caller.Value, now),
            "note" => listing.AddModerationNote(
                _guids.NewSequentialGuid(), caller.Value, reason, now),
            _ => Result.Failure(MarketplaceErrors.ModerationNoteInvalid)
        };

        if (applied.IsFailure)
        {
            return applied;
        }

        // Every decision leaves a note, including the ones that changed a state. A trail that
        // recorded only the explicit notes would be missing the decisions themselves.
        if (!string.Equals(command.Request.Action, "note", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(reason))
        {
            listing.AddModerationNote(
                _guids.NewSequentialGuid(),
                caller.Value,
                $"{command.Request.Action}: {reason}",
                now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
