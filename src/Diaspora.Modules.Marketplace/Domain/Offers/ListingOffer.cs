using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Offers;

/// <summary>
/// A negotiation between one buyer and one seller about one listing.
/// <para>
/// <b>The aggregate is the whole negotiation, not a single offer.</b> Modelling each counter as
/// its own row would make "what is on the table" a query over a table rather than a field, and
/// would make the round ceiling, the turn rule and the single-open-negotiation rule three
/// unenforceable conventions. Here they are invariants.
/// </para>
/// <para>
/// <b>Separate from the listing, keyed by <c>MarketplaceListingId</c>.</b> A popular item attracts
/// hundreds of offers, and a listing that had to load them all to accept the next one would get
/// slower exactly as it got more valuable.
/// </para>
/// <para>
/// <b>The turn rule is the invariant that makes this a negotiation.</b>
/// <see cref="AwaitingParty"/> is whoever did not move last, and every mutator checks it: a seller
/// cannot counter their own counter, and a buyer cannot accept an offer they themselves made. It
/// is also what makes the notification unambiguous — there is exactly one person the platform
/// should be prompting at any moment.
/// </para>
/// <para>
/// <b>Accepting does not sell anything.</b> It settles the negotiation and reserves the stock
/// through the reservation the handler creates; the sale happens when that reservation completes.
/// A module where accepting an offer marked an item sold would be one where a buyer who never
/// turned up still counts as a sale.
/// </para>
/// </summary>
public sealed class ListingOffer : AggregateRoot<ListingOfferId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<NegotiationEntry> _history = [];

    private ListingOffer(
        ListingOfferId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        UserId buyerUserId,
        UserId sellerUserId,
        OfferTerms terms,
        DateTimeOffset? expiresAt,
        DateTimeOffset createdAt)
        : base(id)
    {
        ListingId = listingId;
        SellerId = sellerId;
        BuyerUserId = buyerUserId;
        SellerUserId = sellerUserId;
        Terms = terms;
        ExpiresAt = expiresAt;
        Status = OfferStatus.Pending;
        AwaitingParty = OfferParty.Seller;
        CreatedAt = createdAt;
        LastActivityAt = createdAt;
    }

    private ListingOffer() { }

    // ── Parties & subject ───────────────────────────────────────────────────────────────────

    public MarketplaceListingId ListingId { get; private set; } = default!;

    public SellerProfileId SellerId { get; private set; } = default!;

    public UserId BuyerUserId { get; private set; } = default!;

    /// <summary>
    /// The member behind the seller profile when the negotiation opened.
    /// <para>
    /// Stored rather than resolved, because it is the answer to "whose turn is it" and that
    /// question has to be answerable from this aggregate alone. It is a snapshot: a shop that
    /// changes hands mid-negotiation keeps the negotiation with the person who was answering it.
    /// </para>
    /// </summary>
    public UserId SellerUserId { get; private set; } = default!;

    // ── State ───────────────────────────────────────────────────────────────────────────────

    /// <summary>What is on the table right now.</summary>
    public OfferTerms Terms { get; private set; } = default!;

    public OfferStatus Status { get; private set; }

    /// <summary>Whose turn it is. Meaningless once the negotiation is settled.</summary>
    public OfferParty AwaitingParty { get; private set; }

    /// <summary>When the open offer lapses. Null means it stands until somebody moves.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    /// <summary>Who ended it. Null on an expiry.</summary>
    public UserId? SettledBy { get; private set; }

    public string? SettlementReason { get; private set; }

    /// <summary>The last move's instant. What "stale negotiation" is measured from.</summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    /// <summary>
    /// Whether the seller has answered at all. Feeds their response statistics, and is the one
    /// honest measure of whether a marketplace's sellers are ignoring people.
    /// </summary>
    public bool HasSellerResponded { get; private set; }

    // ── History ─────────────────────────────────────────────────────────────────────────────

    /// <summary>The complete negotiation, in order. Append-only.</summary>
    public IReadOnlyList<NegotiationEntry> History =>
        _history.OrderBy(entry => entry.Round).ToList().AsReadOnly();

    /// <summary>How many moves have been made. What the round ceiling counts.</summary>
    public int Rounds => _history.Count;

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    public bool IsOpen => !IsDeleted && OfferStatusTransitions.IsOpen(Status);

    /// <summary>Whether the clock has run out on the open offer.</summary>
    public bool HasLapsed(DateTimeOffset now) =>
        IsOpen && ExpiresAt is { } expiry && expiry <= now;

    /// <summary>How long the negotiation has run, or ran.</summary>
    public TimeSpan Duration => (SettledAt ?? LastActivityAt) - CreatedAt;

    /// <summary>Whether a member is one of the two parties.</summary>
    public bool Involves(UserId userId) =>
        userId is not null && (BuyerUserId == userId || SellerUserId == userId);

    /// <summary>Which side a member is on. Null when they are neither.</summary>
    public OfferParty? PartyOf(UserId userId) =>
        userId is null ? null
        : BuyerUserId == userId ? OfferParty.Buyer
        : SellerUserId == userId ? OfferParty.Seller
        : null;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens a negotiation with the buyer's first offer.
    /// <para>
    /// The handler has already checked that the listing takes offers, that the currency matches,
    /// that the amount clears the seller's private floor and that the buyer has no other open
    /// negotiation on this listing. What the aggregate enforces is everything that is true of a
    /// negotiation whatever the listing says: an offer needs an author, an expiry in the future,
    /// and a buyer who is not the seller.
    /// </para>
    /// </summary>
    public static Result<ListingOffer> Open(
        ListingOfferId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        UserId buyerUserId,
        UserId sellerUserId,
        OfferTerms terms,
        DateTimeOffset? expiresAt,
        DateTimeOffset now,
        Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(sellerId);
        ArgumentNullException.ThrowIfNull(buyerUserId);
        ArgumentNullException.ThrowIfNull(sellerUserId);
        ArgumentNullException.ThrowIfNull(terms);

        if (buyerUserId == sellerUserId)
        {
            return Result.Failure<ListingOffer>(MarketplaceErrors.OfferSelfBid);
        }

        if (expiresAt is { } expiry && expiry <= now)
        {
            return Result.Failure<ListingOffer>(MarketplaceErrors.OfferExpiryInPast);
        }

        var offer = new ListingOffer(
            id, listingId, sellerId, buyerUserId, sellerUserId, terms, expiresAt, now);

        offer.Record(entryId, NegotiationAction.Offered, OfferParty.Buyer, buyerUserId, terms, now);

        offer.RaiseDomainEvent(new OfferCreatedDomainEvent
        {
            OfferId = id,
            ListingId = listingId,
            SellerId = sellerId,
            BuyerUserId = buyerUserId,
            Amount = terms.Amount,
            CurrencyCode = terms.CurrencyCode,
            Quantity = terms.Quantity,
            ExpiresAt = expiresAt,
            CreatedAt = now
        });

        return Result.Success(offer);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Moves
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Answers with a different number, and hands the turn back.
    /// <para>
    /// Either side may counter, and that symmetry is what makes this a negotiation rather than a
    /// bidding form. The round ceiling is what stops a scripted client running one forever against
    /// a seller who cannot leave it.
    /// </para>
    /// </summary>
    public Result Counter(
        UserId actorId, OfferTerms terms, DateTimeOffset? expiresAt, DateTimeOffset now,
        Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(terms);

        var turn = EnsureTurn(actorId, now);

        if (turn.IsFailure)
        {
            return turn;
        }

        if (Rounds >= MarketplaceLimits.MaxNegotiationRounds)
        {
            return Result.Failure(MarketplaceErrors.OfferRoundLimit);
        }

        if (!string.Equals(terms.CurrencyCode, Terms.CurrencyCode, StringComparison.Ordinal))
        {
            return Result.Failure(MarketplaceErrors.CurrencyMismatch);
        }

        if (expiresAt is { } expiry && expiry <= now)
        {
            return Result.Failure(MarketplaceErrors.OfferExpiryInPast);
        }

        var party = PartyOf(actorId)!.Value;

        Terms = terms;
        Status = OfferStatus.Countered;
        AwaitingParty = party == OfferParty.Buyer ? OfferParty.Seller : OfferParty.Buyer;
        ExpiresAt = expiresAt;

        if (party == OfferParty.Seller)
        {
            NoteSellerResponse();
        }

        Record(entryId, NegotiationAction.Countered, party, actorId, terms, now);

        RaiseDomainEvent(new OfferCounteredDomainEvent
        {
            OfferId = Id,
            ListingId = ListingId,
            SellerId = SellerId,
            BuyerUserId = BuyerUserId,
            CounteredBy = party,
            ActorId = actorId,
            Amount = terms.Amount,
            CurrencyCode = terms.CurrencyCode,
            Round = Rounds,
            CounteredAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Takes the deal. Whoever's turn it is may accept what is in front of them.
    /// <para>
    /// Accepting does not move stock — the handler creates the reservation that does, in the same
    /// unit of work. Doing it here would mean the offer aggregate reaching into the listing, which
    /// is precisely the cross-aggregate write the separation exists to avoid.
    /// </para>
    /// </summary>
    public Result Accept(UserId actorId, DateTimeOffset now, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);

        var turn = EnsureTurn(actorId, now);

        if (turn.IsFailure)
        {
            return turn;
        }

        var party = PartyOf(actorId)!.Value;

        if (party == OfferParty.Seller)
        {
            NoteSellerResponse();
        }

        Settle(OfferStatus.Accepted, NegotiationAction.Accepted, party, actorId, null, now, entryId);

        return Result.Success();
    }

    /// <summary>Declines. The negotiation ends and the history keeps every round of it.</summary>
    public Result Reject(UserId actorId, string? reason, DateTimeOffset now, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);

        var turn = EnsureTurn(actorId, now);

        if (turn.IsFailure)
        {
            return turn;
        }

        var party = PartyOf(actorId)!.Value;

        if (party == OfferParty.Seller)
        {
            NoteSellerResponse();
        }

        Settle(OfferStatus.Rejected, NegotiationAction.Rejected, party, actorId, reason, now, entryId);

        return Result.Success();
    }

    /// <summary>
    /// Takes back an offer.
    /// <para>
    /// <b>Available to whoever moved last, which is the opposite of the turn rule elsewhere.</b>
    /// Withdrawing is retracting your own move, so it is the party who is <em>not</em> being waited
    /// on who may do it — a buyer who found the item elsewhere, a seller who sold it in the
    /// meantime. It must never be blocked: leaving somebody committed to a price they have retracted
    /// is worse than an untidy history.
    /// </para>
    /// </summary>
    public Result Withdraw(UserId actorId, string? reason, DateTimeOffset now, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);

        if (!IsOpen)
        {
            return Result.Failure(MarketplaceErrors.OfferAlreadyDecided);
        }

        var party = PartyOf(actorId);

        if (party is null)
        {
            return Result.Failure(MarketplaceErrors.NotOfferParty);
        }

        Settle(
            OfferStatus.Withdrawn, NegotiationAction.Withdrawn, party.Value, actorId, reason, now,
            entryId);

        return Result.Success();
    }

    /// <summary>
    /// Lapses the negotiation because its clock ran out. The sweep's entry point.
    /// <para>
    /// Idempotent and quiet: an offer that has already settled, or whose expiry has not passed,
    /// succeeds and does nothing. A sweep that failed on rows it had already processed would spend
    /// its whole batch on retries.
    /// </para>
    /// </summary>
    public Result Expire(DateTimeOffset now, Guid entryId)
    {
        if (!HasLapsed(now))
        {
            return Result.Success();
        }

        Settle(
            OfferStatus.Expired, NegotiationAction.Expired, AwaitingParty, actorId: null,
            reason: null, now, entryId);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
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

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The shared guard for a move: the negotiation is open, has not lapsed, and it is this
    /// member's turn.
    /// </summary>
    private Result EnsureTurn(UserId actorId, DateTimeOffset now)
    {
        if (IsDeleted || !IsOpen)
        {
            return Result.Failure(MarketplaceErrors.OfferAlreadyDecided);
        }

        if (ExpiresAt is { } expiry && expiry <= now)
        {
            return Result.Failure(MarketplaceErrors.OfferExpired);
        }

        var party = PartyOf(actorId);

        if (party is null)
        {
            return Result.Failure(MarketplaceErrors.NotOfferParty);
        }

        return party.Value == AwaitingParty
            ? Result.Success()
            : Result.Failure(MarketplaceErrors.OfferNotYours);
    }

    private void Settle(
        OfferStatus status,
        NegotiationAction action,
        OfferParty party,
        UserId? actorId,
        string? reason,
        DateTimeOffset now,
        Guid entryId)
    {
        Status = status;
        SettledAt = now;
        SettledBy = actorId;
        SettlementReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        Record(
            entryId,
            action,
            party,
            actorId,

            // An accepted negotiation keeps its amount, because that is the price of the sale;
            // a rejection or a withdrawal records no amount, because nothing stands.
            action == NegotiationAction.Accepted ? Terms : null,
            now);

        RaiseDomainEvent(new OfferSettledDomainEvent
        {
            OfferId = Id,
            ListingId = ListingId,
            SellerId = SellerId,
            BuyerUserId = BuyerUserId,
            Status = status,
            SettledBy = actorId,
            Amount = status == OfferStatus.Accepted ? Terms.Amount : null,
            CurrencyCode = Terms.CurrencyCode,
            Quantity = Terms.Quantity,
            Reason = SettlementReason,
            Duration = now - CreatedAt,
            SettledAt = now
        });
    }

    private void Record(
        Guid entryId,
        NegotiationAction action,
        OfferParty party,
        UserId? actorId,
        OfferTerms? terms,
        DateTimeOffset now)
    {
        _history.Add(NegotiationEntry.Create(
            entryId,
            _history.Count + 1,
            action,
            party,
            actorId,
            terms?.Amount,
            Terms.CurrencyCode,
            terms?.Quantity ?? Terms.Quantity,
            terms?.Message,
            now));

        LastActivityAt = now;
    }

    private void NoteSellerResponse() => HasSellerResponded = true;
}
