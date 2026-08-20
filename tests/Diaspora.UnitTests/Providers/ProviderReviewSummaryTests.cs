using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Providers;

/// <summary>
/// Ratings, and the rules that keep them honest.
/// <para>
/// The rollup is the number members choose a solicitor from, so the tests that matter are the
/// ones about it being wrong: drift after an edit, drift after a hiding, and a single review
/// outranking a hundred.
/// </para>
/// </summary>
public class ProviderReviewSummaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Reviewer =
        UserId.From(Guid.Parse("00000000-0000-0000-0000-0000000000b1"));

    [Fact]
    public void An_empty_summary_has_no_average_and_no_reviews()
    {
        var summary = ProviderReviewSummary.Empty;

        summary.TotalReviews.Should().Be(0);
        summary.Average.Should().Be(0);
        summary.HasReviews.Should().BeFalse();
    }

    [Fact]
    public void The_average_and_total_are_derived_from_the_distribution()
    {
        // The frontend's own mock: 89 five-star, 28 four, 5 three, 1 two, 1 one — 124 reviews
        // averaging 4.8.
        var summary = ProviderReviewSummary.FromDistribution(1, 1, 5, 28, 89);

        summary.TotalReviews.Should().Be(124);
        summary.Average.Should().Be(4.6);
        summary.CountFor(5).Should().Be(89);
    }

    [Fact]
    public void The_average_is_rounded_to_one_decimal_because_that_is_what_every_screen_renders()
    {
        var summary = ProviderReviewSummary.FromDistribution(0, 0, 1, 1, 1);

        // 4.0 exactly. Storing more precision than anything displays invites two screens to
        // disagree over 4.849999.
        summary.Average.Should().Be(4.0);
    }

    [Fact]
    public void A_summary_rebuilt_from_raw_ratings_matches_one_built_from_buckets()
    {
        var fromRatings = ProviderReviewSummary.FromRatings([5, 5, 4, 3, 5]);
        var fromBuckets = ProviderReviewSummary.FromDistribution(0, 0, 1, 1, 3);

        fromRatings.Should().Be(fromBuckets);
    }

    [Fact]
    public void Ratings_outside_one_to_five_are_ignored_rather_than_counted()
    {
        var summary = ProviderReviewSummary.FromRatings([5, 0, 6, -1, 4]);

        summary.TotalReviews.Should().Be(2);
        summary.Average.Should().Be(4.5);
    }

    [Fact]
    public void A_review_outside_one_to_five_is_refused()
    {
        var written = ProviderReview.Write(
            ProviderReviewId.New(), ProviderId.New(), Reviewer, rating: 6, Now);

        written.IsFailure.Should().BeTrue();
        written.Error.Should().Be(ProviderErrors.ReviewRatingOutOfRange);
    }

    [Fact]
    public void A_body_longer_than_the_clients_own_cap_is_refused()
    {
        var written = ProviderReview.Write(
            ProviderReviewId.New(),
            ProviderId.New(),
            Reviewer,
            rating: 5,
            Now,
            body: new string('x', ProviderLimits.MaxReviewBodyLength + 1));

        written.IsFailure.Should().BeTrue();
        written.Error.Should().Be(ProviderErrors.ReviewBodyTooLong);
    }

    [Fact]
    public void A_review_may_be_revised_inside_the_edit_window()
    {
        var review = Write(5);

        review.Revise(3, null, "Changed my mind.", Now.AddDays(1)).IsSuccess.Should().BeTrue();

        review.Rating.Should().Be(3);
        review.IsEdited.Should().BeTrue();
        review.EditedAt.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void A_review_may_not_be_revised_after_the_edit_window()
    {
        var review = Write(5);

        // A five-star review rewritten to one star a year later, after the provider declined a
        // refund, is leverage rather than feedback.
        var revised = review.Revise(
            1, null, null, Now + ProviderLimits.ReviewEditWindow + TimeSpan.FromDays(1));

        revised.IsFailure.Should().BeTrue();
        revised.Error.Should().Be(ProviderErrors.ReviewEditWindowClosed);
        review.Rating.Should().Be(5);
    }

    [Fact]
    public void Withdrawing_stays_available_forever()
    {
        var review = Write(5);

        review.Withdraw().IsSuccess.Should().BeTrue();

        review.Status.Should().Be(ReviewStatus.Withdrawn);
        review.CountsTowardsSummary.Should().BeFalse();
    }

    [Fact]
    public void Only_published_reviews_count_towards_the_summary()
    {
        var published = Write(5);
        var hidden = Write(1);

        hidden.Hide().IsSuccess.Should().BeTrue();

        published.CountsTowardsSummary.Should().BeTrue();
        hidden.CountsTowardsSummary.Should().BeFalse();
    }

    [Fact]
    public void A_removed_review_cannot_be_republished_in_place()
    {
        var review = Write(1);

        review.Remove().IsSuccess.Should().BeTrue();

        var restored = review.Republish();

        restored.IsFailure.Should().BeTrue();
        restored.Error.Should().Be(ProviderErrors.ReviewRemoved);
    }

    [Fact]
    public void A_hidden_review_can_be_republished_by_the_moderator_who_hid_it()
    {
        var review = Write(4);

        review.Hide().IsSuccess.Should().BeTrue();
        review.Republish().IsSuccess.Should().BeTrue();

        review.Status.Should().Be(ReviewStatus.Published);
        review.CountsTowardsSummary.Should().BeTrue();
    }

    private static ProviderReview Write(int rating) =>
        ProviderReview.Write(
            ProviderReviewId.New(), ProviderId.New(), Reviewer, rating, Now).Value;
}
