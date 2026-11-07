using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Moderation;

/// <summary>
/// Community's report reasons and content types, in the platform's moderation vocabulary
/// (7 Nov 2026). Every Community report, including every guide report, reaches a Trust case through
/// these two maps; a reason that fell through to a default, or a content type filed under a name no
/// module's target answers to, would put the report on the wrong case or on none.
/// </summary>
public sealed class CommunityModerationReasonsTests
{
    [Theory]
    [InlineData(ReportReasonCode.Misinformation, ModerationReasonCode.FalseOrMisleading)]
    [InlineData(ReportReasonCode.Spam, ModerationReasonCode.Spam)]
    [InlineData(ReportReasonCode.Harassment, ModerationReasonCode.OffensiveOrHarassment)]
    [InlineData(ReportReasonCode.Inappropriate, ModerationReasonCode.Other)]
    [InlineData(ReportReasonCode.Outdated, ModerationReasonCode.Other)]
    [InlineData(ReportReasonCode.Copyright, ModerationReasonCode.Copyright)]
    [InlineData(ReportReasonCode.PersonalData, ModerationReasonCode.PersonalData)]
    [InlineData(ReportReasonCode.Other, ModerationReasonCode.Other)]
    public void Each_reason_maps_to_the_platforms(ReportReasonCode reason, ModerationReasonCode expected) =>
        CommunityModerationReasons.Map(reason).Should().Be(expected);

    [Fact]
    public void Every_reason_is_mapped_here()
    {
        // The theory above names each value; a reason added to the enum without a row there fails.
        Enum.GetValues<ReportReasonCode>().Should().HaveCount(8,
            "a new report reason needs a deliberate mapping and a row in the theory above");
    }

    [Fact]
    public void A_vague_inappropriate_report_is_not_a_serious_reason()
    {
        // Ten serious reports hide content before anyone has looked. "Inappropriate" must not be a
        // lever a group can pull; harassment, which is the modal's "abusive", is.
        ModerationReasons.IsSerious(CommunityModerationReasons.Map(ReportReasonCode.Inappropriate))
            .Should().BeFalse();
        ModerationReasons.IsSerious(CommunityModerationReasons.Map(ReportReasonCode.Harassment))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(CommunityContentType.Post, ModerationContentTypes.CommunityPost)]
    [InlineData(CommunityContentType.Comment, ModerationContentTypes.CommunityComment)]
    [InlineData(CommunityContentType.Guide, ModerationContentTypes.Guide)]
    [InlineData(CommunityContentType.Listing, ModerationContentTypes.Provider)]
    [InlineData(CommunityContentType.Job, ModerationContentTypes.Job)]
    [InlineData(CommunityContentType.Housing, ModerationContentTypes.HousingListing)]
    [InlineData(CommunityContentType.Marketplace, ModerationContentTypes.MarketplaceListing)]
    [InlineData(CommunityContentType.Event, ModerationContentTypes.Event)]
    [InlineData(CommunityContentType.Organization, ModerationContentTypes.Organization)]
    [InlineData(CommunityContentType.Poll, "poll")]
    [InlineData(CommunityContentType.Member, "member")]
    [InlineData(CommunityContentType.Message, "message")]
    [InlineData(CommunityContentType.Notice, "notice")]
    [InlineData(CommunityContentType.Other, "other")]
    public void Each_content_type_is_filed_under_the_name_its_moderation_target_answers_to(
        CommunityContentType type, string expected)
    {
        CommunityModerationReasons.ContentTypeFor(type).Should().Be(expected);

        CommunityModerationReasons.TryParseContentType(expected, out var parsed).Should().BeTrue();
        parsed.Should().Be(type, "a decision finds its way back to the Community reports it answers");
    }

    [Fact]
    public void Every_content_type_has_its_own_name()
    {
        var names = Enum.GetValues<CommunityContentType>()
            .Select(CommunityModerationReasons.ContentTypeFor)
            .ToList();

        names.Should().OnlyHaveUniqueItems("two Community types under one name would share a case");
        Enum.GetValues<CommunityContentType>().Should().HaveCount(14,
            "a new content type needs a deliberate name and a row in the theory above");
    }

    [Fact]
    public void A_content_type_Community_never_files_under_is_not_parsed()
    {
        CommunityModerationReasons.TryParseContentType("fundraiser", out _).Should().BeFalse();
        CommunityModerationReasons.TryParseContentType(null, out _).Should().BeFalse();
    }
}
