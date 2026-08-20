using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Conversations;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Conversations;

/// <summary>
/// Opens — or returns — the conversation reference for a listing and a buyer.
/// <para>
/// <b>Idempotent, and it has to be.</b> Asking twice about the same item is not two conversations,
/// and a client that retried after a timeout would otherwise leave the seller with two threads about
/// one chair.
/// </para>
/// <para>
/// <b>The response is a reference, not a thread.</b> Whether a messaging provider ever claims it is
/// somebody else's module's business; on a deployment with none the reference stays requested and
/// the client renders the seller's contact preference, which is exactly what the seller asked for.
/// </para>
/// </summary>
public sealed class StartConversationCommandHandler
    : ICommandHandler<StartConversationCommand, ConversationReferenceResponse>
{
    private readonly IListingConversationRepository _conversations;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public StartConversationCommandHandler(
        IListingConversationRepository conversations,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _conversations = conversations;
        _listings = listings;
        _sellers = sellers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ConversationReferenceResponse>> Handle(
        StartConversationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ConversationReferenceResponse>(caller.Error);
        }

        var listingId = MarketplaceListingId.From(command.ListingId);
        var listing = await _listings.GetForReadAsync(listingId, cancellationToken);

        if (listing is null || !await _authorization.CanSeeAsync(listing, cancellationToken))
        {
            return Result.Failure<ConversationReferenceResponse>(
                MarketplaceErrors.ListingNotFound);
        }

        var seller = await _sellers.GetForReadAsync(listing.SellerId, cancellationToken);

        if (seller is null)
        {
            return Result.Failure<ConversationReferenceResponse>(
                MarketplaceErrors.SellerNotFound);
        }

        var existing = await _conversations.FindAsync(
            listingId, caller.Value, cancellationToken);

        if (existing is not null)
        {
            return Result.Success(Describe(existing, listing, seller));
        }

        var conversation = ListingConversation.Request(
            ListingConversationId.From(_guids.NewSequentialGuid()),
            listingId,
            listing.SellerId,
            caller.Value,
            seller.OwnerUserId,
            command.Request?.Message,
            _clock.UtcNow);

        if (conversation.IsFailure)
        {
            return Result.Failure<ConversationReferenceResponse>(conversation.Error);
        }

        await _conversations.AddAsync(conversation.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(Describe(conversation.Value, listing, seller));
    }

    private static ConversationReferenceResponse Describe(
        ListingConversation conversation,
        MarketplaceListing listing,
        SellerProfile seller) => new()
        {
            ConversationId = conversation.Id.Value,
            ListingId = conversation.ListingId.Value,
            ListingTitle = listing.Title.Value,
            ListingCoverMediaId = listing.CoverImage?.MediaId,
            SellerId = conversation.SellerId.Value,
            SellerName = seller.Identity.DisplayName,
            BuyerUserId = conversation.BuyerUserId.Value,
            Status = conversation.Status.ToString(),
            ExternalConversationId = conversation.ExternalConversationId,
            Provider = conversation.Provider,

            // What the client falls back to when no provider has claimed the reference — which
            // today is always.
            ContactPreference = listing.ContactPreference.ToString(),
            CreatedAt = conversation.CreatedAt,
            LastActivityAt = conversation.LastActivityAt
        };
}
