using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// A community's rules and notices, and a member's preferences — 21 Oct 2026. What each refuses to
/// store, so a half-written row can never reach a screen.
/// </summary>
public class CommunityGovernanceDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    // ── Rules ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_rule_keeps_trimmed_text_and_drops_an_empty_consequence()
    {
        var rule = CommunityRule.Create(
            CommunityRuleId.New(), CommunityId.New(), 1, "  Be kind ", " Respect. ", "   ").Value;

        rule.Title.Should().Be("Be kind");
        rule.Body.Should().Be("Respect.");
        rule.Consequence.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "Body", "rule.title.required")]
    [InlineData("Title", " ", "rule.body.required")]
    public void A_rule_without_a_title_or_body_is_refused(string? title, string? body, string code)
    {
        var created = CommunityRule.Create(CommunityRuleId.New(), CommunityId.New(), 1, title, body, null);

        created.IsFailure.Should().BeTrue();
        created.Error.Code.Should().EndWith(code);
    }

    [Fact]
    public void A_rule_longer_than_its_limit_is_refused()
    {
        var created = CommunityRule.Create(
            CommunityRuleId.New(), CommunityId.New(), 1, "Title", new string('x', CommunityRule.BodyMaximumLength + 1), null);

        created.IsFailure.Should().BeTrue();
        created.Error.Code.Should().EndWith("rule.body.length");
    }

    // ── Notices ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_notice_records_the_role_its_publisher_held()
    {
        var notice = Publish(expiresAt: Now.AddDays(1)).Value;

        notice.PublishedByRole.Should().Be("Moderator");
        notice.IsLive(Now).Should().BeTrue();
        notice.IsLive(Now.AddDays(2)).Should().BeFalse();
    }

    [Fact]
    public void A_notice_cannot_expire_before_it_was_published()
    {
        var published = Publish(expiresAt: Now.AddMinutes(-1));

        published.IsFailure.Should().BeTrue();
        published.Error.Code.Should().EndWith("notice.expiry.in-past");
    }

    // ── Preferences ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Preferences_start_private_with_discovery_on()
    {
        var preferences = CommunityMemberPreferences.CreateFor(UserId.New());

        preferences.CrossCommunityDiscoveryEnabled.Should().BeTrue();
        preferences.ShowPrimaryCommunityOnProfile.Should().BeFalse();
        preferences.ShowInterestedCommunitiesOnProfile.Should().BeFalse();
        preferences.ShowCommunityRolesOnProfile.Should().BeFalse();
        preferences.DefaultMembershipVisibility.Should().Be(CommunityMembershipVisibility.Private);
        preferences.Notifications.Should().BeEmpty();
    }

    [Fact]
    public void Communities_are_kept_only_under_the_specific_scope()
    {
        var preferences = CommunityMemberPreferences.CreateFor(UserId.New());
        var community = Guid.NewGuid();

        var replaced = preferences.Replace(
            true, false, false, false, CommunityMembershipVisibility.Private, "en",
            [
                new CommunityNotificationPreference("events", "specific", [community, community]),
                new CommunityNotificationPreference("jobs", "none", [community])
            ]);

        replaced.IsSuccess.Should().BeTrue();
        preferences.Notifications.Should().HaveCount(2);
        preferences.Notifications[0].CommunityIds.Should().Equal(community);
        preferences.Notifications[1].CommunityIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData("events", "everyone", "preferences.scope.unknown")]
    [InlineData(" ", "none", "preferences.category.required")]
    public void An_unusable_notification_row_is_refused(string category, string scope, string code)
    {
        var replaced = CommunityMemberPreferences.CreateFor(UserId.New()).Replace(
            true, false, false, false, CommunityMembershipVisibility.Private, null,
            [new CommunityNotificationPreference(category, scope, [])]);

        replaced.IsFailure.Should().BeTrue();
        replaced.Error.Code.Should().EndWith(code);
    }

    [Fact]
    public void A_content_language_must_look_like_a_language_tag()
    {
        var replaced = CommunityMemberPreferences.CreateFor(UserId.New()).Replace(
            true, false, false, false, CommunityMembershipVisibility.Private, "not a language", []);

        replaced.IsFailure.Should().BeTrue();
        replaced.Error.Code.Should().EndWith("preferences.language.shape");
    }

    private static Result<CommunityNotice> Publish(DateTimeOffset? expiresAt) =>
        CommunityNotice.Publish(
            CommunityNoticeId.New(),
            CommunityId.New(),
            UserId.New(),
            CommunityMembershipRole.Moderator,
            "Meeting",
            "Saturday.",
            expiresAt,
            isPinned: false,
            Now);
}
