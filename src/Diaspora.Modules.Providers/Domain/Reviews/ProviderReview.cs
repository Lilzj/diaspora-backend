using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Events;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Reviews;

/// <summary>
/// One member's rated account of a provider.
/// <para>
/// <b>An aggregate of its own, not a child of <see cref="Providers.Provider"/>.</b> Three
/// reasons in ascending order of importance: a provider with four thousand reviews must not be
/// loaded to add the four-thousand-and-first; two members reviewing at once must not contend on
/// the provider row; and a review has a lifecycle the provider does not share — it is written,
/// edited, replied to, reported, hidden, appealed and withdrawn, by people who are not the
/// provider. What the provider keeps is the rollup.
/// </para>
/// <para>
/// <b>Why B7 owns this and B6 does not.</b> The brief says not to duplicate Community features,
/// and this is the one place B7 deliberately does not delegate. A review carries a star rating;
/// B6 has no rating concept, and growing one for a single vertical would put a five-point scale
/// on posts, comments and guides that nobody asked for. Everything B6 <em>does</em> model about
/// a provider — comments, reactions, reports, shares, follows, bookmarks — B7 consumes rather
/// than reimplements, through the content-source seam. See D-8 in the frontend contract report.
/// </para>
/// <para>
/// <b>The edit window is a safety feature, not an inconvenience.</b> A review that can be
/// rewritten at any time is leverage: a five-star review edited to one star a year later, after
/// the provider declined a refund, is a pattern every review platform eventually meets.
/// Withdrawal stays available forever, because a member must always be able to take back
/// something they said.
/// </para>
/// </summary>
public sealed class ProviderReview : AggregateRoot<ProviderReviewId>, IAuditableEntity, ISoftDeletable
{
    public const int MaxTitleLength = 160;

    public const int MaxResponseLength = 2_000;

    private ProviderReview(
        ProviderReviewId id,
        ProviderId providerId,
        UserId reviewerId,
        int rating,
        string? title,
        string? body,
        DateTimeOffset createdAt)
        : base(id)
    {
        ProviderId = providerId;
        ReviewerId = reviewerId;
        Rating = rating;
        Title = title;
        Body = body;
        Status = ReviewStatus.Published;
        CreatedAt = createdAt;
    }

    private ProviderReview() { }

    public ProviderId ProviderId { get; private set; } = default!;

    public UserId ReviewerId { get; private set; } = default!;

    /// <summary>One to five. The client's star selector, exactly.</summary>
    public int Rating { get; private set; }

    public string? Title { get; private set; }

    /// <summary>The client's optional body, capped at its own <c>maxLength={500}</c>.</summary>
    public string? Body { get; private set; }

    public ReviewStatus Status { get; private set; }

    /// <summary>The service reviewed, when the member named one. Lets a provider see which
    /// of its offerings people rate well, which one aggregate score cannot.</summary>
    public ProviderServiceId? ServiceId { get; private set; }

    /// <summary>The provider's public reply. One, not a thread — a thread is what B6 is for.</summary>
    public string? Response { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public UserId? RespondedBy { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    /// <summary>True once edited. The client renders this so a reader knows.</summary>
    public bool IsEdited { get; private set; }

    /// <summary>
    /// Whether the reviewer had a recorded interaction with the provider. Not a gate — most
    /// interactions on this platform happen by phone and leave no trace — but a strong ranking
    /// and trust signal, and the honest way to distinguish a customer from a passer-by.
    /// </summary>
    public bool IsVerifiedInteraction { get; private set; }

    /// <summary>Helpful votes. Projected from Community's reactions rather than owned here.</summary>
    public int HelpfulCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>Whether this review counts towards the provider's rating.</summary>
    public bool CountsTowardsSummary => !IsDeleted && Status == ReviewStatus.Published;

    public static Result<ProviderReview> Write(
        ProviderReviewId id,
        ProviderId providerId,
        UserId reviewerId,
        int rating,
        DateTimeOffset now,
        string? title = null,
        string? body = null,
        ProviderServiceId? serviceId = null,
        bool isVerifiedInteraction = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(reviewerId);

        if (rating is < ProviderReviewSummary.MinRating or > ProviderReviewSummary.MaxRating)
        {
            return Result.Failure<ProviderReview>(ProviderErrors.ReviewRatingOutOfRange);
        }

        var trimmedBody = body?.Trim();

        if (trimmedBody is { Length: > ProviderLimits.MaxReviewBodyLength })
        {
            return Result.Failure<ProviderReview>(ProviderErrors.ReviewBodyTooLong);
        }

        var review = new ProviderReview(
            id,
            providerId,
            reviewerId,
            rating,
            Trim(title, MaxTitleLength),
            string.IsNullOrEmpty(trimmedBody) ? null : trimmedBody,
            now)
        {
            ServiceId = serviceId,
            IsVerifiedInteraction = isVerifiedInteraction
        };

        review.RaiseDomainEvent(new ProviderReviewSubmittedDomainEvent
        {
            ReviewId = id,
            ProviderId = providerId,
            ReviewerId = reviewerId,
            Rating = rating,
            HasBody = review.Body is not null
        });

        return Result.Success(review);
    }

    public Result Revise(int rating, string? title, string? body, DateTimeOffset now)
    {
        if (IsDeleted || Status is ReviewStatus.Removed or ReviewStatus.Withdrawn)
        {
            return Result.Failure(ProviderErrors.ReviewRemoved);
        }

        if (now - CreatedAt > ProviderLimits.ReviewEditWindow)
        {
            return Result.Failure(ProviderErrors.ReviewEditWindowClosed);
        }

        if (rating is < ProviderReviewSummary.MinRating or > ProviderReviewSummary.MaxRating)
        {
            return Result.Failure(ProviderErrors.ReviewRatingOutOfRange);
        }

        var trimmedBody = body?.Trim();

        if (trimmedBody is { Length: > ProviderLimits.MaxReviewBodyLength })
        {
            return Result.Failure(ProviderErrors.ReviewBodyTooLong);
        }

        var previousRating = Rating;

        Rating = rating;
        Title = Trim(title, MaxTitleLength);
        Body = string.IsNullOrEmpty(trimmedBody) ? null : trimmedBody;
        EditedAt = now;
        IsEdited = true;

        RaiseChanged(previousRating, Status, Status);

        return Result.Success();
    }

    /// <summary>The reviewer takes their review back. Always available, even after the edit window.</summary>
    public Result Withdraw()
    {
        if (Status == ReviewStatus.Withdrawn)
        {
            return Result.Success();
        }

        var previous = Status;
        Status = ReviewStatus.Withdrawn;

        RaiseChanged(Rating, previous, Status);

        return Result.Success();
    }

    /// <summary>A moderator hides the review, reversibly.</summary>
    public Result Hide()
    {
        if (Status == ReviewStatus.Hidden)
        {
            return Result.Success();
        }

        var previous = Status;
        Status = ReviewStatus.Hidden;

        RaiseChanged(Rating, previous, Status);

        return Result.Success();
    }

    /// <summary>A moderator removes the review for cause. Not restorable in place.</summary>
    public Result Remove()
    {
        if (Status == ReviewStatus.Removed)
        {
            return Result.Success();
        }

        var previous = Status;
        Status = ReviewStatus.Removed;

        RaiseChanged(Rating, previous, Status);

        return Result.Success();
    }

    /// <summary>A moderator un-hides a review they hid.</summary>
    public Result Republish()
    {
        if (Status == ReviewStatus.Published)
        {
            return Result.Success();
        }

        if (Status == ReviewStatus.Removed)
        {
            return Result.Failure(ProviderErrors.ReviewRemoved);
        }

        var previous = Status;
        Status = ReviewStatus.Published;

        RaiseChanged(Rating, previous, Status);

        return Result.Success();
    }

    /// <summary>
    /// The provider replies. One reply rather than a thread, deliberately: a public argument
    /// between an owner and a customer helps nobody, and a threaded conversation is what B6 is
    /// for if the platform ever decides it wants one.
    /// </summary>
    public Result Respond(string? response, UserId respondedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(respondedBy);

        if (IsDeleted || Status == ReviewStatus.Removed)
        {
            return Result.Failure(ProviderErrors.ReviewRemoved);
        }

        var trimmed = response?.Trim();

        if (trimmed is { Length: > MaxResponseLength })
        {
            return Result.Failure(ProviderErrors.ReviewBodyTooLong);
        }

        Response = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        RespondedAt = Response is null ? null : now;
        RespondedBy = Response is null ? null : respondedBy;

        return Result.Success();
    }

    /// <summary>Projected from Community's reactions. Never incremented from inside B7's writes.</summary>
    public void SetHelpfulCount(int count) => HelpfulCount = count < 0 ? 0 : count;

    public void MarkVerifiedInteraction() => IsVerifiedInteraction = true;

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        var previous = Status;

        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        Status = ReviewStatus.Withdrawn;

        RaiseChanged(Rating, previous, Status);
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        Status = ReviewStatus.Published;
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private void RaiseChanged(int previousRating, ReviewStatus previousStatus, ReviewStatus currentStatus)
    {
        if (previousRating == Rating && previousStatus == currentStatus)
        {
            return;
        }

        RaiseDomainEvent(new ProviderReviewChangedDomainEvent
        {
            ReviewId = Id,
            ProviderId = ProviderId,
            PreviousRating = previousRating,
            CurrentRating = Rating,
            PreviousStatus = previousStatus,
            CurrentStatus = currentStatus
        });
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
