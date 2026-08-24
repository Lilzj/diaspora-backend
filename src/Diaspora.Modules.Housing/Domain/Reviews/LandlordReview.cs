using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Reviews;

/// <summary>
/// One tenant's rated account of a tenancy they actually took.
/// <para>
/// <b>Tied to a reservation that reached <see cref="HousingReservationStatus.Occupied"/>.</b>
/// Not <c>LeaseSigned</c>, and the distinction is deliberate: a signed lease is a promise, and
/// this module's own words for <c>Occupied</c> are "moved in — the successful terminal state".
/// A star that sits above <c>Pay £1850 deposit</c> has to mean somebody actually lived there.
/// </para>
/// <para>
/// <b>One review per tenancy, not per landlord.</b> A tenant who takes a second place from the
/// same agency has a second experience and is entitled to report it; collapsing them onto the
/// landlord would silently overwrite the first. It also makes the invariant enforceable by a
/// unique index on a column that already has to be there.
/// </para>
/// <para>
/// <b>An aggregate of its own, not a child of <c>LandlordProfile</c>.</b> An agency with four
/// thousand reviews must not be loaded to add the four-thousand-and-first, two tenants reviewing
/// at once must not contend on the landlord row, and a review has a lifecycle the landlord does
/// not share. What the landlord keeps is the rollup.
/// </para>
/// <para>
/// <b>The edit window is a safety feature.</b> A review that can be rewritten at any time is
/// leverage, and in housing the leverage runs both ways — a deposit is usually still held. A
/// tenant may always withdraw; neither party may rewrite history a year later.
/// </para>
/// </summary>
public sealed class LandlordReview : AggregateRoot<LandlordReviewId>, IAuditableEntity
{
    public const int MaxTitleLength = 160;

    public const int MaxBodyLength = 500;

    public const int MaxResponseLength = 2_000;

    /// <summary>How long a review stays editable. Matches B7 and B10 so the three agree.</summary>
    public static readonly TimeSpan EditWindow = TimeSpan.FromDays(14);

    private LandlordReview(
        LandlordReviewId id,
        LandlordProfileId landlordId,
        UserId reviewerId,
        HousingReservationId reservationId,
        HousingListingId listingId,
        int rating,
        string? title,
        string? body,
        DateTimeOffset createdAt) : base(id)
    {
        LandlordId = landlordId;
        ReviewerId = reviewerId;
        ReservationId = reservationId;
        ListingId = listingId;
        Rating = rating;
        Title = title;
        Body = body;
        Status = LandlordReviewStatus.Published;
        CreatedAt = createdAt;
    }

    private LandlordReview() { }

    public LandlordProfileId LandlordId { get; private set; } = default!;

    public UserId ReviewerId { get; private set; } = default!;

    /// <summary>
    /// The tenancy this review is about. Required, and unique — it is what makes the star mean
    /// "somebody lived here" rather than "somebody has an opinion".
    /// </summary>
    public HousingReservationId ReservationId { get; private set; } = default!;

    /// <summary>
    /// Which property. Carried so a landlord can see which of their places rates well, which one
    /// aggregate score cannot tell them.
    /// </summary>
    public HousingListingId ListingId { get; private set; } = default!;

    /// <summary>One to five.</summary>
    public int Rating { get; private set; }

    public string? Title { get; private set; }

    public string? Body { get; private set; }

    public LandlordReviewStatus Status { get; private set; }

    /// <summary>The landlord's public reply. One, not a thread.</summary>
    public string? Response { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether this review counts towards the landlord's rollup.</summary>
    public bool CountsTowardsSummary => Status == LandlordReviewStatus.Published;

    /// <summary>
    /// Writes a review of a tenancy.
    /// <para>
    /// The caller proves the reservation reached <c>Occupied</c> and belongs to this tenant —
    /// that needs a repository read and so cannot live here. What the aggregate enforces is the
    /// part that is nobody's judgement call: one to five, and never your own listing.
    /// </para>
    /// </summary>
    public static Result<LandlordReview> Write(
        LandlordProfileId landlordId,
        UserId landlordOwnerUserId,
        UserId reviewerId,
        HousingReservationId reservationId,
        HousingListingId listingId,
        int rating,
        string? title,
        string? body,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(landlordId);
        ArgumentNullException.ThrowIfNull(reviewerId);
        ArgumentNullException.ThrowIfNull(reservationId);

        if (reviewerId == landlordOwnerUserId)
        {
            return Result.Failure<LandlordReview>(HousingErrors.ReviewSelf);
        }

        if (rating is < LandlordReviewSummary.MinRating or > LandlordReviewSummary.MaxRating)
        {
            return Result.Failure<LandlordReview>(HousingErrors.ReviewRatingOutOfRange);
        }

        return Result.Success(new LandlordReview(
            LandlordReviewId.New(),
            landlordId,
            reviewerId,
            reservationId,
            listingId,
            rating,
            Truncate(title, MaxTitleLength),
            Truncate(body, MaxBodyLength),
            now));
    }

    /// <summary>Revises a review inside its edit window.</summary>
    public Result Revise(int rating, string? title, string? body, DateTimeOffset now)
    {
        if (Status != LandlordReviewStatus.Published)
        {
            return Result.Failure(HousingErrors.ReviewNotEditable);
        }

        if (now - CreatedAt > EditWindow)
        {
            return Result.Failure(HousingErrors.ReviewEditWindowClosed);
        }

        if (rating is < LandlordReviewSummary.MinRating or > LandlordReviewSummary.MaxRating)
        {
            return Result.Failure(HousingErrors.ReviewRatingOutOfRange);
        }

        Rating = rating;
        Title = Truncate(title, MaxTitleLength);
        Body = Truncate(body, MaxBodyLength);
        EditedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// The landlord's one public reply.
    /// <para>
    /// No window. A landlord answering a two-star review two months later is the system working.
    /// </para>
    /// </summary>
    public Result Respond(string response, DateTimeOffset now)
    {
        if (Status != LandlordReviewStatus.Published)
        {
            return Result.Failure(HousingErrors.ReviewNotEditable);
        }

        var text = Truncate(response, MaxResponseLength);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Failure(HousingErrors.ReviewResponseEmpty);
        }

        Response = text;
        RespondedAt = now;

        return Result.Success();
    }

    /// <summary>Taken back by its author. Stops counting; the row stays for audit.</summary>
    public void Withdraw(DateTimeOffset now)
    {
        Status = LandlordReviewStatus.Withdrawn;
        ModifiedAt = now;
    }

    /// <summary>Taken down by a moderator. Stops counting; recoverable.</summary>
    public void Hide(DateTimeOffset now)
    {
        Status = LandlordReviewStatus.Hidden;
        ModifiedAt = now;
    }

    /// <summary>Restored after an appeal. Counts again.</summary>
    public void Restore(DateTimeOffset now)
    {
        Status = LandlordReviewStatus.Published;
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
