using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Reviews;

/// <summary>
/// One buyer's rated account of a completed purchase.
/// <para>
/// <b>Tied to a reservation, not merely to a seller.</b> This is the difference between this
/// aggregate and B7's <see cref="Providers.ProviderReview"/>, which it otherwise follows
/// closely, and it is the whole point: a provider review is an opinion anyone may hold, whereas
/// a marketplace rating sits directly above <c>Pay £600 with Diaspora</c>. A star there has to
/// mean somebody actually bought something and can say how it went. <see cref="ReservationId"/>
/// is required and the handler checks it reached <see cref="ReservationStatus.Completed"/> —
/// the state B10 documents as "handed over… the only state that turns held stock into sold
/// stock".
/// </para>
/// <para>
/// <b>One review per reservation, not per seller.</b> A buyer who comes back twice has two
/// separate experiences and is entitled to report both; collapsing them onto the seller would
/// silently overwrite the first. It also makes the invariant enforceable by a unique index on a
/// column that already has to be there.
/// </para>
/// <para>
/// <b>An aggregate of its own, not a child of <c>SellerProfile</c>.</b> A seller with four
/// thousand reviews must not be loaded to add the four-thousand-and-first, two buyers reviewing
/// at once must not contend on the seller row, and a review has a lifecycle the seller does not
/// share — written, edited, replied to, hidden, withdrawn, by people who are not the seller.
/// What the seller keeps is the rollup.
/// </para>
/// <para>
/// <b>The edit window is a safety feature, not an inconvenience.</b> A review that can be
/// rewritten at any time is leverage: five stars edited to one a year later, after the seller
/// declined a refund, is a pattern every marketplace eventually meets. Withdrawal stays
/// available forever, because a member must always be able to take back something they said.
/// </para>
/// </summary>
public sealed class SellerReview : AggregateRoot<SellerReviewId>, IAuditableEntity
{
    public const int MaxTitleLength = 160;

    public const int MaxBodyLength = 500;

    public const int MaxResponseLength = 2_000;

    /// <summary>How long a review stays editable. Matches B7's window so the two agree.</summary>
    public static readonly TimeSpan EditWindow = TimeSpan.FromDays(14);

    private SellerReview(
        SellerReviewId id,
        SellerProfileId sellerId,
        UserId reviewerId,
        ListingReservationId reservationId,
        MarketplaceListingId listingId,
        int rating,
        string? title,
        string? body,
        DateTimeOffset createdAt) : base(id)
    {
        SellerId = sellerId;
        ReviewerId = reviewerId;
        ReservationId = reservationId;
        ListingId = listingId;
        Rating = rating;
        Title = title;
        Body = body;
        Status = SellerReviewStatus.Published;
        CreatedAt = createdAt;
    }

    private SellerReview() { }

    public SellerProfileId SellerId { get; private set; } = default!;

    public UserId ReviewerId { get; private set; } = default!;

    /// <summary>
    /// The completed purchase this review is about. Required, and unique — it is what makes the
    /// star mean "somebody bought this" rather than "somebody has an opinion".
    /// </summary>
    public ListingReservationId ReservationId { get; private set; } = default!;

    /// <summary>
    /// What was bought. Carried so a seller can see which of their listings rates well, which
    /// one aggregate score cannot tell them.
    /// </summary>
    public MarketplaceListingId ListingId { get; private set; } = default!;

    /// <summary>One to five. The client's star selector, exactly.</summary>
    public int Rating { get; private set; }

    public string? Title { get; private set; }

    public string? Body { get; private set; }

    public SellerReviewStatus Status { get; private set; }

    /// <summary>The seller's public reply. One, not a thread — a thread is what B6 is for.</summary>
    public string? Response { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether this review counts towards the seller's rollup.</summary>
    public bool CountsTowardsSummary => Status == SellerReviewStatus.Published;

    /// <summary>
    /// Writes a review of a completed purchase.
    /// <para>
    /// The caller is responsible for proving the reservation completed and belongs to this
    /// buyer — that needs a repository read and so cannot live here. What the aggregate does
    /// enforce is the part that is nobody's judgement call: the rating is one to five, and a
    /// seller cannot review themselves.
    /// </para>
    /// </summary>
    public static Result<SellerReview> Write(
        SellerProfileId sellerId,
        UserId sellerOwnerUserId,
        UserId reviewerId,
        ListingReservationId reservationId,
        MarketplaceListingId listingId,
        int rating,
        string? title,
        string? body,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sellerId);
        ArgumentNullException.ThrowIfNull(reviewerId);
        ArgumentNullException.ThrowIfNull(reservationId);

        if (reviewerId == sellerOwnerUserId)
        {
            return Result.Failure<SellerReview>(MarketplaceErrors.ReviewSelf);
        }

        if (rating is < SellerReviewSummary.MinRating or > SellerReviewSummary.MaxRating)
        {
            return Result.Failure<SellerReview>(MarketplaceErrors.ReviewRatingOutOfRange);
        }

        return Result.Success(new SellerReview(
            SellerReviewId.New(),
            sellerId,
            reviewerId,
            reservationId,
            listingId,
            rating,
            Truncate(title, MaxTitleLength),
            Truncate(body, MaxBodyLength),
            now));
    }

    /// <summary>
    /// Revises a review inside its edit window.
    /// <para>
    /// Refused once the window closes rather than silently ignored, so a member finds out their
    /// change did not land instead of believing it did.
    /// </para>
    /// </summary>
    public Result Revise(int rating, string? title, string? body, DateTimeOffset now)
    {
        if (Status != SellerReviewStatus.Published)
        {
            return Result.Failure(MarketplaceErrors.ReviewNotEditable);
        }

        if (now - CreatedAt > EditWindow)
        {
            return Result.Failure(MarketplaceErrors.ReviewEditWindowClosed);
        }

        if (rating is < SellerReviewSummary.MinRating or > SellerReviewSummary.MaxRating)
        {
            return Result.Failure(MarketplaceErrors.ReviewRatingOutOfRange);
        }

        Rating = rating;
        Title = Truncate(title, MaxTitleLength);
        Body = Truncate(body, MaxBodyLength);
        EditedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// The seller's one public reply.
    /// <para>
    /// Always available, with no window. A seller answering a two-star review two months later
    /// is the system working; the leverage the edit window guards against runs the other way.
    /// </para>
    /// </summary>
    public Result Respond(string response, DateTimeOffset now)
    {
        if (Status != SellerReviewStatus.Published)
        {
            return Result.Failure(MarketplaceErrors.ReviewNotEditable);
        }

        var text = Truncate(response, MaxResponseLength);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Failure(MarketplaceErrors.ReviewResponseEmpty);
        }

        Response = text;
        RespondedAt = now;

        return Result.Success();
    }

    /// <summary>Taken back by its author. Stops counting; the row stays for audit.</summary>
    public void Withdraw(DateTimeOffset now)
    {
        Status = SellerReviewStatus.Withdrawn;
        ModifiedAt = now;
    }

    /// <summary>Taken down by a moderator. Stops counting; recoverable.</summary>
    public void Hide(DateTimeOffset now)
    {
        Status = SellerReviewStatus.Hidden;
        ModifiedAt = now;
    }

    /// <summary>Restored after an appeal. Counts again.</summary>
    public void Restore(DateTimeOffset now)
    {
        Status = SellerReviewStatus.Published;
        ModifiedAt = now;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
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
