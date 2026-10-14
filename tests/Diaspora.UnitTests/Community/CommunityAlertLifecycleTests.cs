using Diaspora.Modules.Community;
using Diaspora.Modules.Community.Application.Posts;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Community;

/// <summary>
/// An alert's lifecycle beyond publish and expire — 16 Sep 2026.
/// <para>
/// A member who changed their route for a reported road closure needs to learn that the report was
/// withdrawn, or that the road has reopened. Before this, the only author action was
/// <c>Archive</c>, which made the alert vanish from every feed without saying anything — the one
/// outcome a member who acted on it cannot tell apart from the alert never having existed.
/// </para>
/// </summary>
public class CommunityAlertLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    // ── Withdraw and cancel ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Withdrawing_keeps_the_alert_readable_and_says_when()
    {
        var post = Published();

        post.Withdraw(post.AuthorId, Now.AddHours(1)).IsSuccess.Should().BeTrue();

        post.Status.Should().Be(PostStatus.Withdrawn);
        post.ClosedAt.Should().Be(Now.AddHours(1));
        post.IsClosed.Should().BeTrue();
        post.IsVisibleToOthers.Should().BeTrue(
            "a member who acted on the alert must be able to read that it was withdrawn");
        post.DomainEvents.OfType<PostWithdrawnDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Cancelling_records_its_own_status_and_event()
    {
        var post = Published();

        post.Cancel(post.AuthorId, Now.AddHours(2)).IsSuccess.Should().BeTrue();

        post.Status.Should().Be(PostStatus.Cancelled);
        post.ClosedAt.Should().Be(Now.AddHours(2));
        post.DomainEvents.OfType<PostCancelledDomainEvent>().Should().ContainSingle();
        post.DomainEvents.OfType<PostWithdrawnDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Only_the_author_may_withdraw_or_cancel()
    {
        var post = Published();

        post.Withdraw(UserId.New(), Now).Error.Should().Be(CommunityErrors.PostNotOwned);
        post.Cancel(UserId.New(), Now).Error.Should().Be(CommunityErrors.PostNotOwned);
        post.Status.Should().Be(PostStatus.Published);
    }

    [Fact]
    public void An_alert_that_is_not_in_effect_cannot_be_withdrawn_or_cancelled()
    {
        var draft = Draft();
        draft.Withdraw(draft.AuthorId, Now).Error.Should().Be(CommunityErrors.PostNotOpen);

        var expired = Published();
        expired.Expire(Now.AddDays(3));
        expired.Cancel(expired.AuthorId, Now.AddDays(3)).Error.Should().Be(CommunityErrors.PostNotOpen);

        var withdrawn = Published();
        withdrawn.Withdraw(withdrawn.AuthorId, Now);
        withdrawn.Cancel(withdrawn.AuthorId, Now).Error.Should().Be(
            CommunityErrors.PostNotOpen, "a withdrawn alert has nothing left to call off");
    }

    [Fact]
    public void A_withdrawn_alert_cannot_be_rewritten()
    {
        var post = Published();
        post.Withdraw(post.AuthorId, Now);

        post.Edit(PostBody.Create("It was fine after all").Value, null, post.AuthorId, null, Now.AddHours(1))
            .Error.Should().Be(CommunityErrors.PostNotEditable,
                "changing the words of a retracted report would change what members acted on");
    }

    // ── The affected area ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_affected_area_is_a_radius_around_the_pin()
    {
        var post = Draft(radius: 800);

        post.AffectedRadiusMetres.Should().Be(800);
    }

    [Fact]
    public void An_affected_area_without_a_pin_is_refused()
    {
        var draft = CommunityPost.Draft(
            UserId.New(),
            PublishingIdentity.Personal(),
            PostBody.Create("Flooding somewhere in town").Value,
            null,
            PostCategory.Traffic,
            UrgencyLevel.Urgent,
            ContentAudience.Public,
            PostLocationContext.Create(countryCode: "GB", cityName: "London"),
            Now,
            affectedRadiusMetres: 800);

        draft.Error.Should().Be(CommunityErrors.AffectedAreaNeedsPoint);
    }

    /// <summary>
    /// The case that was accepted before this rule existed: an alert naming only "London" is placed
    /// at London's centre, and a radius around that centre claims a precise area nobody chose.
    /// </summary>
    [Fact]
    public void An_affected_area_around_the_centre_of_a_named_place_is_refused()
    {
        var draft = CommunityPost.Draft(
            UserId.New(),
            PublishingIdentity.Personal(),
            PostBody.Create("Power cut").Value,
            null,
            PostCategory.Traffic,
            UrgencyLevel.Urgent,
            ContentAudience.Public,
            PostLocationContext.Create(
                countryCode: "GB", cityName: "London", latitude: 51.5074d, longitude: -0.1278d),
            Now,
            affectedRadiusMetres: 2_000);

        draft.Error.Should().Be(CommunityErrors.AffectedAreaNeedsPoint);
    }

    [Theory]
    [InlineData(CommunityLimits.MinAffectedRadiusMetres - 1)]
    [InlineData(CommunityLimits.MaxAffectedRadiusMetres + 1)]
    public void An_affected_area_a_map_cannot_draw_honestly_is_refused(int radius)
    {
        var draft = CommunityPost.Draft(
            UserId.New(),
            PublishingIdentity.Personal(),
            PostBody.Create("Road closed").Value,
            null,
            PostCategory.Traffic,
            UrgencyLevel.Urgent,
            ContentAudience.Public,
            Peckham(),
            Now,
            affectedRadiusMetres: radius);

        draft.IsFailure.Should().BeTrue();
        draft.Error.Code.Should().Be(CommunityErrorCodes.AffectedRadiusOutOfRange);
    }

    // ── Amending ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Amending_changes_urgency_and_reach_and_is_marked_after_the_silent_window()
    {
        var post = Published();

        post.AmendAlert(UrgencyLevel.Emergency, 5_000, post.AuthorId, Now.AddMinutes(30))
            .IsSuccess.Should().BeTrue();

        post.Urgency.Should().Be(UrgencyLevel.Emergency);
        post.AffectedRadiusMetres.Should().Be(5_000);
        post.IsEdited.Should().BeTrue("members who planned around the first version must see it changed");
        post.EditedAt.Should().Be(Now.AddMinutes(30));
        post.DomainEvents.OfType<PostAlertAmendedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Zero_removes_the_affected_area_and_null_leaves_it_alone()
    {
        var post = Published(radius: 1_000);

        post.AmendAlert(null, null, post.AuthorId, Now.AddMinutes(1));
        post.AffectedRadiusMetres.Should().Be(1_000);

        post.AmendAlert(null, 0, post.AuthorId, Now.AddMinutes(2));
        post.AffectedRadiusMetres.Should().BeNull();
    }

    [Fact]
    public void An_amendment_that_changes_nothing_is_not_news()
    {
        var post = Published(radius: 1_000);
        post.ClearDomainEvents();

        post.AmendAlert(UrgencyLevel.Urgent, 1_000, post.AuthorId, Now.AddHours(1)).IsSuccess.Should().BeTrue();

        post.IsEdited.Should().BeFalse();
        post.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void A_closed_alert_cannot_be_amended()
    {
        var post = Published();
        post.Cancel(post.AuthorId, Now);

        post.AmendAlert(UrgencyLevel.Low, null, post.AuthorId, Now.AddMinutes(1))
            .Error.Should().Be(CommunityErrors.PostNotOpen);
    }

    // ── Who may publish an Emergency ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The rule decided on 16 Sep 2026, checked where urgency is known. The route's policy admits
    /// anybody who may post at all, so a handler that forgot this check would publish the Emergency.
    /// </summary>
    [Fact]
    public async Task A_member_without_the_emergency_permission_is_refused_an_emergency_alert()
    {
        var caller = FakeCurrentUser.Authenticated();
        caller.Permissions = [CommunityPermissions.Post];

        // The refusal comes before anything is read or written, so nothing else is needed.
        var handler = new CreatePostCommandHandler(null!, null!, null!, null!, null!, caller, null!, null!, null!);

        var result = await handler.Handle(
            new CreatePostCommand
            {
                Request = new CreatePostRequest
                {
                    Body = "Gas leak, evacuate the block",
                    Urgency = "emergency",
                    CountryCode = "GB",
                    Latitude = 51.4742d,
                    Longitude = -0.0691d
                }
            },
            CancellationToken.None);

        result.Error.Should().Be(CommunityErrors.EmergencyNotPermitted);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static PostLocationContext Peckham() =>
        PostLocationContext.Create(
            countryCode: "GB", latitude: 51.4742d, longitude: -0.0691d, pointIsExact: true);

    private static CommunityPost Draft(int? radius = null) =>
        CommunityPost.Draft(
            UserId.New(),
            PublishingIdentity.Personal(),
            PostBody.Create("Road closed between the station and the market.").Value,
            null,
            PostCategory.Traffic,
            UrgencyLevel.Urgent,
            ContentAudience.Public,
            Peckham(),
            Now,
            affectedRadiusMetres: radius).Value;

    private static CommunityPost Published(int? radius = null)
    {
        var post = Draft(radius);
        post.Publish(Now).IsSuccess.Should().BeTrue();

        return post;
    }
}
