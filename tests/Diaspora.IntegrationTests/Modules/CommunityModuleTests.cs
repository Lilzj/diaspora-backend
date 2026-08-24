using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Community &amp; Social Foundation Engine, over real HTTP through the same host every other
/// suite uses.
/// <para>
/// Three things are under test at once. The module's behaviour. That a module with twenty-six
/// tables, fifteen permissions and thirty-four routes composed into the platform through nothing
/// but contributor interfaces and one project reference. And the thing that makes B6 different
/// from every module before it: that its social capabilities are addressed by a
/// <em>content reference</em> rather than a post identifier, so the same endpoints will serve
/// Guides, Community Updates and Community Life the day those modules register a content source.
/// </para>
/// </summary>
public sealed class CommunityModuleTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_community_feature_gates_composed_into_the_platform_catalogue()
    {
        // Proof the module's IFeatureContributor reached the composed catalogue: gates it owns
        // turn up in Identity's response, which the Community module never touched.
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain("community.feed");
        names.Should().Contain("community.posting");
        names.Should().Contain("community.commenting");
    }

    [Fact]
    public async Task The_platform_manifest_lists_the_community_module()
    {
        var response = await HttpClient.GetAsync("/");

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Community");
    }

    [Fact]
    public async Task The_capability_report_names_the_live_registries()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await HttpClient.GetAsync("/api/v1/community/capabilities"));

        // Read from the registries, not from constants — a deployment that adds a feed says so
        // without a release.
        capabilities.Feeds.Select(feed => feed.Key).Should().Contain(
            ["home", "nearby", "trending", "country", "neighborhood", "saved", "following"]);

        capabilities.Rankings.Should().Contain(
            ["chronological", "trending", "trust-weighted", "location-weighted"]);

        capabilities.Reactions.Select(reaction => reaction.Key).Should().Contain(
            ["like", "helpful", "confirmed", "disputed"]);

        capabilities.SupportedContentTypes.Should().Contain(["post", "comment"]);

        capabilities.MaxPostLength.Should().Be(500);

        capabilities.AutomatedReviewAvailable.Should().BeFalse(
            "no review provider ships, and the report says so rather than implying moderation");
    }

    [Fact]
    public async Task The_report_reason_catalogue_is_served_rather_than_hardcoded_by_the_client()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var reasons = await ReadDataAsync<List<ReasonDto>>(
            await HttpClient.GetAsync("/api/v1/community/reports/reasons"));

        reasons.Select(reason => reason.Code).Should().Contain(
            ["misinformation", "spam", "harassment", "outdated", "personalData"]);

        reasons.Single(reason => reason.Code == "misinformation")
            .RequiresEvidence.Should().BeTrue(
                "a misinformation report whose whole claim is a contradiction needs the link");
    }

    // ── Authorisation ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_community_is_not_readable_without_a_token()
    {
        var response = await HttpClient.GetAsync("/api/v1/community/feeds/home");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_ordinary_member_cannot_reach_the_moderation_queue()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/community/moderation/queue");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Posting ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_verified_member_can_publish_and_read_back_a_post()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var created = await ReadDataAsync<PostDto>(await CreatePostAsync(
            "Document check reported near Peckham High Street. Expect delays."));

        created.PostId.Should().NotBeEmpty();
        created.UpdateId.Should().Be(created.PostId, "the client's own name for the same id");
        created.Status.Should().Be("published");
        created.Category.Should().Be("traffic");
        created.VerificationStatus.Should().Be("unverified");
        created.RequiredConfirmations.Should().Be(3);
        created.Description.Should().Be(created.Body, "the client reads the body as description");

        var fetched = await ReadDataAsync<PostDto>(
            await HttpClient.GetAsync($"/api/v1/community/posts/{created.PostId}"));

        fetched.PostId.Should().Be(created.PostId);
        fetched.CanEdit.Should().BeTrue("the author is reading their own post");
    }

    [Fact]
    public async Task A_post_longer_than_the_composers_counter_is_refused_with_a_stable_code()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await CreatePostAsync(new string('x', 501));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadAsStringAsync();
        problem.Should().Contain("community.post.body-too-long");
    }

    [Fact]
    public async Task A_draft_is_readable_by_its_author_and_carries_no_publication_time()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var draft = await ReadDataAsync<PostDto>(await CreatePostAsync(
            "Half-written thought.", publish: false));

        draft.Status.Should().Be("draft");
        draft.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task Editing_a_post_writes_a_revision_the_history_screen_can_read()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var created = await ReadDataAsync<PostDto>(await CreatePostAsync("First wording."));

        await HttpClient.PutAsJsonAsync(
            $"/api/v1/community/posts/{created.PostId}",
            new { body = "Second wording.", changeSummary = "Fixed the street name" });

        var revisions = await ReadDataAsync<List<RevisionDto>>(
            await HttpClient.GetAsync($"/api/v1/community/posts/{created.PostId}/revisions"));

        revisions.Should().HaveCountGreaterThanOrEqualTo(2);

        revisions.Should().Contain(revision => revision.Content == "First wording.");

        revisions.Should().Contain(
            revision => revision.ChangeSummary.Contains("Fixed the street name"));
    }

    [Fact]
    public async Task A_member_cannot_edit_someone_elses_post()
    {
        var (_, authorAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(authorAuth.Tokens.AccessToken);

        var created = await ReadDataAsync<PostDto>(await CreatePostAsync("Mine."));

        var (_, otherAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(otherAuth.Tokens.AccessToken);

        var response = await HttpClient.PutAsJsonAsync(
            $"/api/v1/community/posts/{created.PostId}", new { body = "Not yours." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Feeds ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_published_post_appears_in_the_home_feed()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var created = await ReadDataAsync<PostDto>(await CreatePostAsync("Visible to everyone."));

        var feed = await ReadDataAsync<FeedDto>(
            await HttpClient.GetAsync("/api/v1/community/feeds/home"));

        feed.FeedKey.Should().Be("home");
        feed.Items.Should().Contain(item => item.ContentId == created.PostId);
    }

    [Fact]
    public async Task An_unknown_feed_key_is_reported_as_not_found_rather_than_empty()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/community/feeds/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await response.Content.ReadAsStringAsync()).Should().Contain("community.feed.unknown");
    }

    [Fact]
    public async Task The_nearby_feed_refuses_to_answer_without_somewhere_to_look()
    {
        // Silently degrading to a global feed is indistinguishable, from the client's side, from
        // a very busy neighbourhood.
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/community/feeds/nearby");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("community.feed.scope-required");
    }

    [Fact]
    public async Task The_feed_accepts_the_second_name_the_client_uses_for_the_radius()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            "/api/v1/community/feeds/nearby?latitude=51.47&longitude=-0.07&distanceRadius=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_feed_page_carries_a_cursor_shaped_response()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var feed = await ReadDataAsync<FeedDto>(
            await HttpClient.GetAsync("/api/v1/community/feeds/home?limit=1"));

        feed.RankingKey.Should().NotBeNullOrWhiteSpace();
        feed.Should().NotBeNull();
    }

    // ── Comments ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_member_can_comment_and_reply_and_the_thread_comes_back_nested()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("Worth discussing."));

        var comment = await ReadDataAsync<CommentDto>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/posts/{post.PostId}/comments",
                new { body = "I can add something here." }));

        comment.Depth.Should().Be(0);

        var reply = await ReadDataAsync<CommentDto>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/comments/{comment.CommentId}/replies",
                new { body = "Agreed." }));

        reply.Depth.Should().Be(1);
        reply.ParentCommentId.Should().Be(comment.CommentId);

        var thread = await ReadDataAsync<ThreadDto>(
            await HttpClient.GetAsync($"/api/v1/community/posts/{post.PostId}/comments"));

        thread.TotalCount.Should().Be(2);

        var root = thread.Comments.Single(candidate => candidate.CommentId == comment.CommentId);
        root.Replies.Should().ContainSingle().Which.CommentId.Should().Be(reply.CommentId);
    }

    [Fact]
    public async Task A_deleted_comment_is_tombstoned_rather_than_removed_from_the_thread()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("Thread."));

        var comment = await ReadDataAsync<CommentDto>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/posts/{post.PostId}/comments", new { body = "Retracted." }));

        await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/comments/{comment.CommentId}/replies", new { body = "Answering." });

        (await HttpClient.DeleteAsync($"/api/v1/community/comments/{comment.CommentId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var thread = await ReadDataAsync<ThreadDto>(
            await HttpClient.GetAsync($"/api/v1/community/posts/{post.PostId}/comments"));

        var tombstoned = thread.Comments.Single(
            candidate => candidate.CommentId == comment.CommentId);

        tombstoned.IsTombstoned.Should().BeTrue();
        tombstoned.Body.Should().BeEmpty();
        tombstoned.Replies.Should().ContainSingle("the reply must not vanish with its parent");
    }

    [Fact]
    public async Task Commenting_on_a_content_type_no_module_owns_is_refused()
    {
        // Accepting it would produce a row nothing can render, moderate or notify about.
        //
        // **This assertion has now gone stale twice**, and for the same reason both times: a
        // content type becomes commentable the moment its module registers a content source,
        // which is the whole point of the ContentReference model. It was written against
        // 'guide', which B8 claimed; it was moved to 'job' on the grounds that "B11 has not
        // shipped", and B11 shipped `JobContentSource` — so the call started answering 404 (a
        // real type, no such job) instead of 400, and the test caught a drift it was never
        // about.
        //
        // **Every declared type now has an owner except one.** Guides, jobs, housing,
        // marketplace, events, organizations, polls, messages and provider listings are all
        // claimed by their modules, and `member` is claimed by Community's own
        // `MemberContentSource` — so there is no real vertical left to point this at, and any
        // choice of one would be a third stale assertion waiting to happen.
        //
        // `Other` is the exception and cannot drift, because it is not a vertical: the enum
        // defines it as "a content type a later module introduced before this enum caught up".
        // It is the sentinel for the unclaimed, so nothing will ever register a source for it,
        // which makes it the one honest subject for this property.
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // The premise, asserted rather than assumed. If some later module does register a
        // source for `member`, this fails here with the reason instead of silently becoming a
        // test about something else.
        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await HttpClient.GetAsync("/api/v1/community/capabilities"));

        capabilities.SupportedContentTypes.Should().NotContain(
            "other", "the case under test only exists while no module owns this type");

        var response = await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/content/other/{Guid.NewGuid()}/comments",
            new { body = "Nobody owns this, so nothing could render the comment." });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("community.content.type-unsupported");
    }

    // ── Reactions ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reacting_returns_the_new_counts_so_a_client_reconciles_in_one_round_trip()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("React to this."));

        var reaction = await ReadDataAsync<ReactionDto>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/posts/{post.PostId}/reactions",
                new { reactionKey = "helpful" }));

        reaction.Summary.HelpfulCount.Should().Be(1);
        reaction.Summary.ViewerReactions.Should().Contain("helpful");
    }

    [Fact]
    public async Task The_same_reaction_twice_is_a_conflict_an_optimistic_client_can_ignore()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("Double tap."));

        await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/posts/{post.PostId}/reactions", new { reactionKey = "helpful" });

        var second = await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/posts/{post.PostId}/reactions", new { reactionKey = "helpful" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await second.Content.ReadAsStringAsync()).Should().Contain("community.reaction.duplicate");
    }

    [Fact]
    public async Task A_reaction_kind_nobody_offers_is_refused()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("Catalogue check."));

        var response = await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/posts/{post.PostId}/reactions",
            new { reactionKey = "invented-by-a-client" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("community.reaction.unknown-kind");
    }

    [Fact]
    public async Task An_author_cannot_confirm_their_own_post()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("My own claim."));

        var response = await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/posts/{post.PostId}/reactions", new { reactionKey = "confirmed" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("community.reaction.self-corroboration");
    }

    [Fact]
    public async Task Withdrawing_a_reaction_takes_the_count_back_down()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("Toggle me."));

        await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/posts/{post.PostId}/reactions", new { reactionKey = "like" });

        var withdrawn = await ReadDataAsync<ReactionDto>(
            await HttpClient.DeleteAsync(
                $"/api/v1/community/posts/{post.PostId}/reactions/like"));

        withdrawn.Summary.LikeCount.Should().Be(0);
        withdrawn.Summary.ViewerReactions.Should().BeEmpty();
    }

    // ── Bookmarks ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_and_unsaving_works_and_the_collections_screen_reflects_it()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("Save me."));

        var saved = await ReadDataAsync<BookmarkDto>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/community/bookmarks",
                new { contentType = "post", contentId = post.PostId }));

        saved.SavedId.Should().Be(saved.BookmarkId, "the client uses both spellings");

        var collections = await ReadDataAsync<List<CollectionDto>>(
            await HttpClient.GetAsync("/api/v1/community/bookmarks/collections"));

        collections.Single(collection => collection.ContentType == "post").Count.Should().Be(1);

        (await HttpClient.DeleteAsync($"/api/v1/community/bookmarks/post/{post.PostId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await ReadDataAsync<List<CollectionDto>>(
            await HttpClient.GetAsync("/api/v1/community/bookmarks/collections"));

        after.Single(collection => collection.ContentType == "post").Count.Should().Be(0);
    }

    [Fact]
    public async Task The_saved_screen_offers_a_tile_for_every_addressable_type_even_at_zero()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var collections = await ReadDataAsync<List<CollectionDto>>(
            await HttpClient.GetAsync("/api/v1/community/bookmarks/collections"));

        collections.Should().NotBeEmpty();
        collections.Should().OnlyContain(collection => collection.IsAvailable);
    }

    // ── Reporting ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_member_can_report_someone_elses_post_using_the_modals_own_category_key()
    {
        var (_, authorAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(authorAuth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("Contested claim."));

        var (_, reporterAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporterAuth.Tokens.AccessToken);

        var report = await ReadDataAsync<ReportDto>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/community/reports",
                new
                {
                    contentType = "communityUpdate",
                    contentId = post.PostId,
                    reason = "conflicting",
                    description = "This contradicts the council notice.",
                    evidenceUrl = "https://example.test/notice"
                }));

        report.ReasonCode.Should().Be(
            "misinformation", "the modal's conflicting key maps onto the reason catalogue");

        report.Status.Should().Be("open");
    }

    [Fact]
    public async Task A_member_cannot_report_their_own_post()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("Mine to delete."));

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/community/reports",
            new { contentType = "post", contentId = post.PostId, reason = "spam" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("community.report.self-content");
    }

    [Fact]
    public async Task The_same_member_cannot_report_the_same_post_twice()
    {
        var (_, authorAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(authorAuth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("Reported once."));

        var (_, reporterAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporterAuth.Tokens.AccessToken);

        var payload = new { contentType = "post", contentId = post.PostId, reason = "spam" };

        await HttpClient.PostAsJsonAsync("/api/v1/community/reports", payload);

        var second = await HttpClient.PostAsJsonAsync("/api/v1/community/reports", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await second.Content.ReadAsStringAsync()).Should().Contain("community.report.duplicate");
    }

    // ── Follows and shares ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Following_and_unfollowing_moves_the_follower_count()
    {
        var (_, targetAuth) = await RegisterVerifiedMemberAsync();
        var targetId = targetAuth.User.Id;

        var (_, followerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(followerAuth.Tokens.AccessToken);

        var follow = await ReadDataAsync<FollowDto>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/follows/{targetId}", new { targetType = "member" }));

        follow.FollowerCount.Should().Be(1);

        var unfollow = await ReadDataAsync<FollowDto>(
            await HttpClient.DeleteAsync($"/api/v1/community/follows/{targetId}"));

        unfollow.FollowerCount.Should().Be(0);
    }

    [Fact]
    public async Task A_member_cannot_follow_themselves()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/follows/{auth.User.Id}", new { targetType = "member" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await response.Content.ReadAsStringAsync()).Should().Contain("community.follow.self");
    }

    [Fact]
    public async Task An_external_share_is_recorded_with_its_self_declared_destination()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(await CreatePostAsync("Pass it on."));

        var share = await ReadDataAsync<ShareDto>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/posts/{post.PostId}/shares",
                new { kind = "external", destination = "WhatsApp" }));

        share.Kind.Should().Be("external");
        share.Destination.Should().Be("whatsapp", "destinations are folded so reports group");
        share.ShareCount.Should().Be(1);
    }

    // ── Hashtags ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_hashtag_in_a_body_is_extracted_normalised_and_becomes_a_feed()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var post = await ReadDataAsync<PostDto>(
            await CreatePostAsync("Delays again on the #LagosTraffic route this morning."));

        post.Hashtags.Should().Contain("lagostraffic");

        var feed = await ReadDataAsync<FeedDto>(
            await HttpClient.GetAsync("/api/v1/community/feeds/hashtag?hashtag=LagosTraffic"));

        feed.Items.Should().Contain(item => item.ContentId == post.PostId);
    }

    [Fact]
    public async Task Trending_and_autocomplete_read_the_stored_window()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await CreatePostAsync("Long queues at the #VisaCentre today.");

        var suggestions = await ReadDataAsync<List<HashtagDto>>(
            await HttpClient.GetAsync("/api/v1/community/hashtags/suggest?prefix=visa"));

        suggestions.Should().Contain(hashtag => hashtag.Slug == "visacentre");

        var trending = await ReadDataAsync<List<HashtagDto>>(
            await HttpClient.GetAsync("/api/v1/community/hashtags/trending"));

        trending.Should().Contain(hashtag => hashtag.Slug == "visacentre");
    }

    // ── Polls ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_poll_hides_its_results_until_the_reader_has_voted()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new
            {
                body = "Does the 345 still run past ten?",
                category = "Transportation",
                poll = new
                {
                    question = "Does the 345 still run past ten?",
                    options = new[] { new { label = "Yes" }, new { label = "No" } }
                }
            });

        var post = await ReadDataAsync<PostDto>(response);

        post.ContentType.Should().Be("poll");
        post.Poll.Should().NotBeNull();
        post.Poll!.CanSeeResults.Should().BeFalse("showing a tally before a vote moves the vote");
        post.Poll.TotalVotes.Should().BeNull("null is the truth; zero would be a claim");

        var optionId = post.Poll.Options[0].OptionId;

        var voted = await ReadDataAsync<PollDto>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/posts/{post.PostId}/poll/votes",
                new { optionIds = new[] { optionId } }));

        voted.HasVoted.Should().BeTrue();
        voted.CanSeeResults.Should().BeTrue();
        voted.TotalVotes.Should().Be(1);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> CreatePostAsync(string body, bool publish = true) =>
        HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body, category = "Traffic", urgency = "Normal", publish });

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record PostDto(
        Guid PostId,
        Guid UpdateId,
        string Body,
        string Description,
        string Category,
        string Status,
        string ContentType,
        string VerificationStatus,
        int RequiredConfirmations,
        bool CanEdit,
        DateTimeOffset? PublishedAt,
        List<string> Hashtags,
        PollDto? Poll);

    private sealed record PollDto(
        Guid PollId,
        List<PollOptionDto> Options,
        bool HasVoted,
        bool CanSeeResults,
        int? TotalVotes);

    private sealed record PollOptionDto(Guid OptionId, string Label, int? VoteCount);

    private sealed record RevisionDto(int VersionNumber, string Content, List<string> ChangeSummary);

    private sealed record CommentDto(
        Guid CommentId,
        Guid? ParentCommentId,
        string Body,
        int Depth,
        bool IsTombstoned,
        List<CommentDto> Replies);

    private sealed record ThreadDto(List<CommentDto> Comments, int TotalCount);

    private sealed record ReactionDto(string ReactionKey, ReactionSummaryDto Summary);

    private sealed record ReactionSummaryDto(
        int TotalCount, int LikeCount, int HelpfulCount, List<string> ViewerReactions);

    private sealed record FeedDto(string FeedKey, string RankingKey, List<FeedItemDto> Items);

    private sealed record FeedItemDto(string ContentType, Guid ContentId);

    private sealed record BookmarkDto(Guid BookmarkId, Guid SavedId, string ContentType);

    private sealed record CollectionDto(
        string? ContentType, string Label, int Count, bool IsAvailable);

    private sealed record ReportDto(Guid ReportId, string ReasonCode, string Status);

    private sealed record FollowDto(Guid FollowId, int FollowerCount);

    private sealed record ShareDto(Guid ShareId, string Kind, string? Destination, int ShareCount);

    private sealed record HashtagDto(string Slug, string DisplayText, int UsageCount);

    private sealed record CapabilitiesDto(
        List<ReactionDefinitionDto> Reactions,
        List<FeedDescriptorDto> Feeds,
        List<string> Rankings,
        List<string> SupportedContentTypes,
        int MaxPostLength,
        bool AutomatedReviewAvailable);

    private sealed record ReactionDefinitionDto(string Key, string Label, string Sentiment);

    private sealed record FeedDescriptorDto(string Key, string DefaultRanking);

    private sealed record ReasonDto(string Code, string Label, bool RequiresEvidence);
}
