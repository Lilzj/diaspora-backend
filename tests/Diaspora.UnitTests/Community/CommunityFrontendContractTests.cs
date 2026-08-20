using Diaspora.Modules.Community;

using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Community;

/// <summary>
/// Pins B6's contract against the Expo client's own literals.
/// <para>
/// Every assertion here cites something the client already ships. When one of these fails the
/// fix is almost never to change the test: it is either to restore the backend name, or — if the
/// change is deliberate — to update the client, the test and the contract freeze together, in
/// one reviewed change. That is what frozen means here.
/// </para>
/// </summary>
public class CommunityFrontendContractTests
{
    // ── Enum literals ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Traffic")]
    [InlineData("Police")]
    [InlineData("Immigration")]
    [InlineData("Weather")]
    [InlineData("Education")]
    [InlineData("Healthcare")]
    [InlineData("Shopping")]
    [InlineData("MarketPrices")]
    [InlineData("Employment")]
    [InlineData("Emergency")]
    [InlineData("Community")]
    [InlineData("Transportation")]
    [InlineData("Utilities")]
    [InlineData("Other")]
    public void Every_update_category_the_client_declares_is_reachable(string clientLiteral)
    {
        // src/types/update.ts — UpdateCategory, and the icon and label maps keyed by it.
        CommunityWireNames.Parse<PostCategory>(clientLiteral, "category")
            .IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("Emergency")]
    [InlineData("Urgent")]
    [InlineData("Normal")]
    [InlineData("Low")]
    public void Every_urgency_the_composer_offers_is_reachable(string clientLiteral) =>
        CommunityWireNames.Parse<UrgencyLevel>(clientLiteral, "urgency")
            .IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("Unverified")]
    [InlineData("CommunityVerified")]
    [InlineData("Disputed")]
    public void Every_verification_status_the_client_renders_is_reachable(string clientLiteral) =>
        CommunityWireNames.Parse<PostVerificationStatus>(clientLiteral, "status")
            .IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("Active")]
    [InlineData("Hidden")]
    [InlineData("Removed")]
    [InlineData("Reported")]
    public void Every_comment_status_the_client_declares_is_reachable(string clientLiteral) =>
        CommunityWireNames.Parse<CommentStatus>(clientLiteral, "status")
            .IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("Misinformation")]
    [InlineData("Spam")]
    [InlineData("Harassment")]
    [InlineData("Inappropriate")]
    [InlineData("Outdated")]
    [InlineData("Copyright")]
    [InlineData("PersonalData")]
    [InlineData("Other")]
    public void Every_report_reason_code_the_client_declares_is_reachable(string clientLiteral) =>
        CommunityWireNames.ParseReportReason(clientLiteral).IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Hide")]
    [InlineData("Archive")]
    [InlineData("Pin")]
    [InlineData("Unpin")]
    [InlineData("Suspend")]
    [InlineData("Restore")]
    public void Every_moderation_action_the_client_declares_is_reachable(string clientLiteral) =>
        CommunityWireNames.Parse<ModerationActionType>(clientLiteral, "action")
            .IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("spam", ContentReviewFlag.Spam)]
    [InlineData("hate_speech", ContentReviewFlag.HateSpeech)]
    [InlineData("offensive_language", ContentReviewFlag.OffensiveLanguage)]
    [InlineData("duplicate", ContentReviewFlag.Duplicate)]
    [InlineData("personal_data", ContentReviewFlag.PersonalData)]
    [InlineData("copyright", ContentReviewFlag.Copyright)]
    [InlineData("low_quality", ContentReviewFlag.LowQuality)]
    public void The_clients_snake_case_review_flags_parse(string clientLiteral, ContentReviewFlag expected)
    {
        // The client spells these in lower snake_case while spelling every sibling enum in
        // PascalCase. Both forms resolve; the wire form is camelCase like every other module.
        CommunityWireNames.Parse<ContentReviewFlag>(clientLiteral, "flag").Value.Should().Be(expected);
    }

    // ── The report modal's contradiction ─────────────────────────────────────────────────

    [Theory]
    [InlineData("conflicting", ReportReasonCode.Misinformation)]
    [InlineData("inaccurate", ReportReasonCode.Outdated)]
    [InlineData("spam", ReportReasonCode.Spam)]
    [InlineData("abusive", ReportReasonCode.Harassment)]
    [InlineData("other", ReportReasonCode.Other)]
    public void The_report_modals_five_keys_map_onto_the_eight_reason_codes(
        string modalKey, ReportReasonCode expected)
    {
        // components/ReportModal.tsx declares five lower-case category keys; types/moderation.ts
        // declares eight PascalCase reason codes. The modal is what submits. Both are accepted
        // and the eight are the catalogue.
        CommunityWireNames.ParseReportReason(modalKey).Value.Should().Be(expected);
    }

    // ── Names the client uses for content types ──────────────────────────────────────────

    [Theory]
    [InlineData("CommunityUpdate")]
    [InlineData("Update")]
    [InlineData("Post")]
    public void The_clients_names_for_a_post_all_resolve_to_one_type(string clientLiteral) =>
        CommunityWireNames.ParseContentType(clientLiteral).Value
            .Should().Be(CommunityContentType.Post);

    [Theory]
    [InlineData("Guide")]
    [InlineData("Listing")]
    [InlineData("Job")]
    [InlineData("Housing")]
    [InlineData("Marketplace")]
    [InlineData("Event")]
    [InlineData("Organization")]
    [InlineData("Comment")]
    public void Every_saved_content_type_the_client_declares_is_addressable(string clientLiteral) =>
        CommunityWireNames.ParseContentType(clientLiteral).IsSuccess.Should().BeTrue();

    // ── Numbers the client renders to members ────────────────────────────────────────────

    [Theory]
    [InlineData(UrgencyLevel.Low, 3, 1)]
    [InlineData(UrgencyLevel.Normal, 3, 1)]
    [InlineData(UrgencyLevel.Urgent, 7, 3)]
    [InlineData(UrgencyLevel.Emergency, 10, 4)]
    public void The_confirmation_thresholds_match_the_clients(
        UrgencyLevel urgency, int total, int verified)
    {
        // src/types/update.ts — VERIFICATION_THRESHOLDS. Rendered as "3 of 7 confirmations".
        ConfirmationThresholds.For(urgency).Should().Be((total, verified));
    }

    [Theory]
    [InlineData(PostCategory.Traffic, 4)]
    [InlineData(PostCategory.Police, 6)]
    [InlineData(PostCategory.Emergency, 24)]
    [InlineData(PostCategory.MarketPrices, 24)]
    [InlineData(PostCategory.Weather, 12)]
    [InlineData(PostCategory.Immigration, 168)]
    [InlineData(PostCategory.Community, 168)]
    [InlineData(PostCategory.Education, 720)]
    [InlineData(PostCategory.Employment, 720)]
    [InlineData(PostCategory.Healthcare, 168)]
    [InlineData(PostCategory.Utilities, 12)]
    [InlineData(PostCategory.Shopping, 24)]
    [InlineData(PostCategory.Transportation, 12)]
    [InlineData(PostCategory.Other, 24)]
    public void The_default_expiries_match_the_clients(PostCategory category, int hours)
    {
        // src/types/update.ts — DEFAULT_EXPIRATION_HOURS.
        PostExpiryPolicy.DefaultHours(category).Should().Be(hours);
    }

    [Fact]
    public void The_post_body_ceiling_matches_the_composers_character_counter()
    {
        // app/update/create.tsx renders {message.length}/500 and sets maxLength={500}.
        PostBody.MaxLength.Should().Be(500);
    }

    [Fact]
    public void The_attachment_ceiling_matches_the_pickers_slot_count()
    {
        // The composer renders four upload slots.
        CommunityLimits.MaxPostAttachments.Should().Be(4);
    }

    [Fact]
    public void The_page_ceiling_matches_the_shared_cursor_pagination_maximum()
    {
        CommunityLimits.MaxFeedPageSize.Should()
            .Be(Diaspora.SharedKernel.Pagination.CursorPaginationRequest.MaxLimit);
    }

    // ── Wire casing ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Enums_go_out_camel_cased_like_every_other_module()
    {
        CommunityWireNames.ToWire(PostCategory.MarketPrices).Should().Be("marketPrices");
        CommunityWireNames.ToWire(UrgencyLevel.Emergency).Should().Be("emergency");
        CommunityWireNames.ToWire(PostVerificationStatus.CommunityVerified)
            .Should().Be("communityVerified");
        CommunityWireNames.ToWire(ContentReviewFlag.HateSpeech).Should().Be("hateSpeech");
    }

    // ── Feed and ranking keys ────────────────────────────────────────────────────────────

    [Fact]
    public void Every_feed_the_client_renders_has_a_key()
    {
        var keys = new[]
        {
            FeedKeys.Home, FeedKeys.Nearby, FeedKeys.Trending, FeedKeys.Country,
            FeedKeys.Neighborhood, FeedKeys.Contributor, FeedKeys.Saved, FeedKeys.Hashtag
        };

        keys.Should().OnlyContain(key => string.Equals(key, key.ToLowerInvariant(), StringComparison.Ordinal));
        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_ranking_keys_the_brief_names_all_exist()
    {
        new[]
        {
            RankingKeys.Chronological,
            RankingKeys.Trending,
            RankingKeys.TrustWeighted,
            RankingKeys.LocationWeighted
        }.Should().OnlyHaveUniqueItems();
    }

    // ── Frozen absence ───────────────────────────────────────────────────────────────────

    [Fact]
    public void An_attachment_response_carries_no_storage_url()
    {
        // ADR-005, and the same frozen absence Media asserts on its own reference response.
        // The client's imageUrls[] is a rendering concern; this module stores identifiers.
        var properties = typeof(Diaspora.Modules.Community.Contracts.V1.Responses.PostAttachmentResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        properties.Should().Contain("MediaId");

        properties.Should().NotContain(
            name => name.Equals("Url", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("StorageKey", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("ThumbnailUrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_post_response_serves_both_the_canonical_name_and_the_clients()
    {
        var properties = typeof(Diaspora.Modules.Community.Contracts.V1.Responses.CommunityPostResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        properties.Should().Contain(["PostId", "UpdateId", "Body", "Description"]);

        properties.Should().Contain([
            "ConfirmationCount",
            "VerifiedResidentConfirmationCount",
            "RequiredConfirmations",
            "VerificationStatus",
            "CommentCount"
        ]);
    }

    [Fact]
    public void The_comment_response_keeps_the_clients_like_fields()
    {
        var properties = typeof(Diaspora.Modules.Community.Contracts.V1.Responses.CommentResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        properties.Should().Contain(["LikeCount", "IsLikedByUser", "IsPinned", "IsEdited"]);
    }
}
