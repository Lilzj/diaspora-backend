using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;
using MediaId = Diaspora.SharedKernel.Identity.MediaId;

namespace Diaspora.UnitTests.Community;

/// <summary>The invariants the post aggregate is responsible for.</summary>
public class CommunityPostTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_draft_is_not_visible_and_carries_no_publication_time()
    {
        var post = Draft();

        post.Status.Should().Be(PostStatus.Draft);
        post.PublishedAt.Should().BeNull();
        post.IsVisibleToOthers.Should().BeFalse();
    }

    [Fact]
    public void Publishing_starts_the_clock_and_captures_the_first_revision()
    {
        var post = Draft();

        post.Publish(Now).IsSuccess.Should().BeTrue();

        post.Status.Should().Be(PostStatus.Published);
        post.PublishedAt.Should().Be(Now);
        post.ExpiresAt.Should().Be(Now.AddHours(PostExpiryPolicy.DefaultHours(PostCategory.Traffic)));
        post.Revisions.Should().ContainSingle().Which.RevisionNumber.Should().Be(1);
    }

    [Fact]
    public void Publishing_twice_is_refused()
    {
        var post = Draft();
        post.Publish(Now);

        post.Publish(Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void An_edit_inside_the_silent_window_does_not_mark_the_post_edited()
    {
        var post = Draft();
        post.Publish(Now);

        post.Edit(Body("Corrected"), null, post.AuthorId, null, Now.AddMinutes(2));

        post.IsEdited.Should().BeFalse("a five-minute typo fix should not carry a permanent badge");
        post.RevisionCount.Should().Be(2);
    }

    [Fact]
    public void An_edit_after_the_silent_window_marks_the_post_edited()
    {
        var post = Draft();
        post.Publish(Now);

        post.Edit(Body("Rewritten"), null, post.AuthorId, null, Now.AddHours(1));

        post.IsEdited.Should().BeTrue();
    }

    [Fact]
    public void A_revision_holds_the_text_as_it_was_not_as_it_became()
    {
        var post = Draft("Original wording");
        post.Publish(Now);
        post.Edit(Body("New wording"), null, post.AuthorId, "Fixed the street name", Now.AddHours(1));

        post.Body.Value.Should().Be("New wording");

        post.Revisions.Should().Contain(revision => revision.Body == "Original wording");
    }

    [Fact]
    public void The_content_type_is_the_richest_thing_the_post_carries()
    {
        var post = Draft();

        post.Attach(CommunityAttachment.ForMedia(AttachmentKind.Image, MediaId.New(), 0));
        post.Attach(CommunityAttachment.ForLink(LinkPreview.Bare("https://example.test"), 1));

        post.Publish(Now);

        post.ContentType.Should().Be(
            PostContentType.Image, "an image with a link is an image post, not a link post");
    }

    [Fact]
    public void Attachments_beyond_the_limit_are_refused()
    {
        var post = Draft();

        for (var index = 0; index < CommunityLimits.MaxPostAttachments; index++)
        {
            post.Attach(CommunityAttachment.ForMedia(AttachmentKind.Image, MediaId.New(), index))
                .IsSuccess.Should().BeTrue();
        }

        post.Attach(CommunityAttachment.ForMedia(AttachmentKind.Image, MediaId.New(), 99))
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Corroboration_below_the_threshold_leaves_the_post_unverified()
    {
        var post = Draft();
        post.Publish(Now);

        var (total, verified) = ConfirmationThresholds.For(UrgencyLevel.Normal);

        for (var index = 0; index < total - 1; index++)
        {
            post.RecordCorroboration(index < verified, 1);
        }

        post.VerificationStatus.Should().Be(PostVerificationStatus.Unverified);
    }

    [Fact]
    public void Reaching_both_thresholds_makes_the_post_community_verified_and_says_so()
    {
        var post = Draft();
        post.Publish(Now);
        post.ClearDomainEvents();

        var (total, verified) = ConfirmationThresholds.For(UrgencyLevel.Normal);

        for (var index = 0; index < total; index++)
        {
            post.RecordCorroboration(index < verified, 1);
        }

        post.VerificationStatus.Should().Be(PostVerificationStatus.CommunityVerified);

        post.DomainEvents.OfType<PostVerificationChangedDomainEvent>()
            .Should().ContainSingle()
            .Which.Current.Should().Be(PostVerificationStatus.CommunityVerified);
    }

    [Fact]
    public void The_total_alone_is_not_enough_without_verified_residents()
    {
        var post = Draft();
        post.Publish(Now);

        var (total, _) = ConfirmationThresholds.For(UrgencyLevel.Normal);

        for (var index = 0; index < total + 5; index++)
        {
            post.RecordCorroboration(byVerifiedResident: false, 1);
        }

        post.VerificationStatus.Should().Be(
            PostVerificationStatus.Unverified,
            "a handful of unverified accounts must not be able to corroborate a safety claim");
    }

    [Fact]
    public void A_heavily_disputed_post_reads_as_disputed_even_when_heavily_confirmed()
    {
        var post = Draft();
        post.Publish(Now);

        var (total, verified) = ConfirmationThresholds.For(UrgencyLevel.Normal);

        for (var index = 0; index < total; index++)
        {
            post.RecordCorroboration(index < verified, 1);
        }

        post.RecordDispute(ConfirmationThresholds.DisputeThreshold(post.ConfirmationCount));

        post.VerificationStatus.Should().Be(
            PostVerificationStatus.Disputed,
            "showing a contested post as verified is the most damaging reading of the numbers");
    }

    [Fact]
    public void Counters_never_go_negative()
    {
        var post = Draft();

        post.ApplyEngagement(counters => counters.WithComments(-5));

        post.Counters.CommentCount.Should().Be(0);
    }

    [Fact]
    public void Deleting_marks_the_post_and_raises_an_event_rather_than_removing_the_row()
    {
        var post = Draft();
        post.Publish(Now);
        post.ClearDomainEvents();

        post.MarkDeleted(Now, "someone");

        post.IsDeleted.Should().BeTrue();
        post.Status.Should().Be(PostStatus.Deleted);
        post.DomainEvents.OfType<PostDeletedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Restoring_returns_a_published_post_to_published_and_a_draft_to_draft()
    {
        var published = Draft();
        published.Publish(Now);
        published.MarkDeleted(Now, null);
        published.Restore();
        published.Status.Should().Be(PostStatus.Published);

        var draft = Draft();
        draft.MarkDeleted(Now, null);
        draft.Restore();
        draft.Status.Should().Be(PostStatus.Draft);
    }

    private static CommunityPost Draft(string body = "Checkpoint on the high street.") =>
        CommunityPost.Draft(
            UserId.New(),
            PublishingIdentity.Personal(),
            Body(body),
            null,
            PostCategory.Traffic,
            UrgencyLevel.Normal,
            ContentAudience.Public,
            PostLocationContext.None,
            Now).Value;

    private static PostBody Body(string value) => PostBody.Create(value).Value;
}
