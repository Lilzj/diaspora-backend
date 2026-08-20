using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Conversations;

/// <summary>
/// The fact that a buyer and a seller want to talk about a listing.
/// <para>
/// <b>Marketplace does not own chat, and this class is the whole of what it does own.</b> The
/// brief is explicit — real-time messaging belongs to a future Communication module — and the
/// temptation this aggregate exists to resist is storing "just one message" so the buyer can ask
/// about collection. One message becomes a thread, a thread becomes read receipts, and a module
/// that owns none of that ends up owning all of it.
/// </para>
/// <para>
/// <b>What it holds is a reference: two parties, one listing, and a slot for whatever identifier a
/// messaging provider issues.</b> <see cref="ExternalConversationId"/> is null on every deployment
/// today, and a Communication module claims it through the conversation seam without a schema
/// change and without an edit here.
/// </para>
/// <para>
/// <b>Keyed by (listing, buyer), so asking twice returns the same reference.</b> A second
/// conversation about the same item between the same two people is not a second conversation.
/// </para>
/// </summary>
public sealed class ListingConversation : AggregateRoot<ListingConversationId>, IAuditableEntity
{
    private ListingConversation(
        ListingConversationId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        UserId buyerUserId,
        UserId sellerUserId,
        DateTimeOffset createdAt)
        : base(id)
    {
        ListingId = listingId;
        SellerId = sellerId;
        BuyerUserId = buyerUserId;
        SellerUserId = sellerUserId;
        Status = ConversationStatus.Requested;
        CreatedAt = createdAt;
        LastActivityAt = createdAt;
    }

    private ListingConversation() { }

    public MarketplaceListingId ListingId { get; private set; } = default!;

    public SellerProfileId SellerId { get; private set; } = default!;

    public UserId BuyerUserId { get; private set; } = default!;

    public UserId SellerUserId { get; private set; } = default!;

    public ConversationStatus Status { get; private set; }

    /// <summary>
    /// Whatever handle the messaging provider issued. Null until one claims the reference, which
    /// on today's deployments is never — and the module works anyway.
    /// </summary>
    public string? ExternalConversationId { get; private set; }

    /// <summary>Which provider claimed it, so two never fight over one reference.</summary>
    public string? Provider { get; private set; }

    /// <summary>The buyer's opening question, if the client sent one with the request.</summary>
    public string? OpeningNote { get; private set; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsOpen => Status != ConversationStatus.Closed;

    public bool Involves(UserId userId) =>
        userId is not null && (BuyerUserId == userId || SellerUserId == userId);

    public static Result<ListingConversation> Request(
        ListingConversationId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        UserId buyerUserId,
        UserId sellerUserId,
        string? openingNote,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(sellerId);
        ArgumentNullException.ThrowIfNull(buyerUserId);
        ArgumentNullException.ThrowIfNull(sellerUserId);

        if (buyerUserId == sellerUserId)
        {
            return Result.Failure<ListingConversation>(MarketplaceErrors.ConversationSelf);
        }

        var trimmed = string.IsNullOrWhiteSpace(openingNote) ? null : openingNote.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxNoteLength })
        {
            trimmed = trimmed[..MarketplaceLimits.MaxNoteLength];
        }

        var conversation = new ListingConversation(
            id, listingId, sellerId, buyerUserId, sellerUserId, now)
        {
            OpeningNote = trimmed
        };

        conversation.RaiseDomainEvent(new ConversationRequestedDomainEvent
        {
            ConversationId = id,
            ListingId = listingId,
            SellerId = sellerId,
            BuyerUserId = buyerUserId,
            SellerUserId = sellerUserId,
            RequestedAt = now
        });

        return Result.Success(conversation);
    }

    /// <summary>
    /// A messaging provider takes ownership and hands back its own identifier.
    /// <para>
    /// Idempotent on the provider: a retry after a timeout re-records the same handle rather than
    /// creating a second conversation the buyer cannot see.
    /// </para>
    /// </summary>
    public Result Attach(string? provider, string? externalConversationId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(externalConversationId))
        {
            return Result.Failure(MarketplaceErrors.ConversationUnavailable);
        }

        var handle = externalConversationId.Trim();

        if (handle.Length > MarketplacePersistenceLimits.ExternalReference)
        {
            handle = handle[..MarketplacePersistenceLimits.ExternalReference];
        }

        Provider = provider.Trim();
        ExternalConversationId = handle;
        Status = ConversationStatus.Open;
        LastActivityAt = now;

        return Result.Success();
    }

    /// <summary>Records that something happened in the conversation, without knowing what.</summary>
    public void Touch(DateTimeOffset now) => LastActivityAt = now;

    /// <summary>Ends it — the listing sold, or a party closed it. Idempotent.</summary>
    public Result Close(DateTimeOffset now)
    {
        if (Status == ConversationStatus.Closed)
        {
            return Result.Success();
        }

        Status = ConversationStatus.Closed;
        ClosedAt = now;
        LastActivityAt = now;

        return Result.Success();
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
