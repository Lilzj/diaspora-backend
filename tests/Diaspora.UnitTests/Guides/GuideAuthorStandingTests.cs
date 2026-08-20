using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The local projection of what Trust has said about an author.
/// <para>
/// Guides stores no verification logic; this row is fed only by Trust integration events so a
/// guide detail page can render "Approved Contributor" without a cross-module call per author
/// per page. Delivery is at-least-once and unordered, so every write is guarded on the event's
/// own timestamp.
/// </para>
/// <para>
/// <b>The guard is per fact, and that is the whole design.</b> One shared "last updated"
/// timestamp would mean a badge event that arrives a second late suppresses the trust-level
/// change that arrived a second early — two independent facts from two independent Trust
/// workflows, silently cancelling each other. Each fact keeps its own timestamp so they cannot.
/// </para>
/// </summary>
public class GuideAuthorStandingTests
{
    private static readonly DateTimeOffset Early = GuideLifecycleTests.Now;
    private static readonly DateTimeOffset Late = Early.AddDays(7);

    [Fact]
    public void A_new_standing_claims_nothing_about_the_author()
    {
        var standing = NewStanding();

        standing.IsApprovedContributor.Should().BeFalse();
        standing.IsCommunityGuide.Should().BeFalse();
        standing.TrustLevel.Should().BeNull();
        standing.Badges.Should().BeEmpty();
        standing.UpdatedAt.Should().BeNull();
    }

    // ── Per-fact guards ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_redelivered_year_old_approval_cannot_undo_last_weeks_removal()
    {
        var standing = NewStanding();

        standing.ApplyContributorApproval(isApproved: true, Early).IsSuccess.Should().BeTrue();
        standing.ApplyContributorApproval(isApproved: false, Late).IsSuccess.Should().BeTrue();

        var replayed = standing.ApplyContributorApproval(isApproved: true, Early);

        replayed.IsSuccess.Should().BeTrue("the event was handled correctly, so it must not be retried forever");
        standing.IsApprovedContributor.Should().BeFalse(
            "a badge Trust withdrew must not come back because a broker replayed a queue");
    }

    [Fact]
    public void An_event_bearing_the_same_instant_as_the_stored_fact_is_dropped()
    {
        var standing = NewStanding();

        standing.ApplyContributorApproval(isApproved: true, Early);
        standing.ApplyContributorApproval(isApproved: false, Early);

        standing.IsApprovedContributor.Should().BeTrue(
            "equal timestamps cannot be ordered, so the stored decision wins");
    }

    [Fact]
    public void Each_fact_keeps_its_own_timestamp_so_the_three_cannot_suppress_each_other()
    {
        var standing = NewStanding();

        standing.ApplyContributorApproval(isApproved: true, Late);
        standing.ApplyCommunityGuideApproval(isCommunityGuide: true, Early);
        standing.ApplyTrustLevel("Trusted", Early);

        standing.ContributorChangedAt.Should().Be(Late);
        standing.CommunityGuideChangedAt.Should().Be(Early);
        standing.TrustLevelChangedAt.Should().Be(Early);

        standing.IsApprovedContributor.Should().BeTrue();
        standing.IsCommunityGuide.Should().BeTrue(
            "an earlier community-guide event is not stale merely because a contributor event " +
            "arrived first");
        standing.TrustLevel.Should().Be("Trusted");
    }

    [Fact]
    public void An_out_of_order_badge_event_does_not_suppress_a_later_trust_level_change()
    {
        // Two independent Trust workflows. If they shared one guard, whichever arrived second
        // would be dropped and the author's rendered standing would depend on network luck.
        var standing = NewStanding();

        standing.AwardBadge("Community Guide", "DE", Late).IsSuccess.Should().BeTrue();

        standing.UpdatedAt.Should().Be(Late);

        var levelChange = standing.ApplyTrustLevel("Established", Early.AddDays(1));

        levelChange.IsSuccess.Should().BeTrue();
        standing.TrustLevel.Should().Be("Established",
            "the trust level has never been set, so nothing about it is stale");

        standing.Badges.Should().ContainSingle().Which.Badge.Should().Be("Community Guide");
    }

    [Fact]
    public void The_observability_timestamp_only_ever_moves_forward()
    {
        var standing = NewStanding();

        standing.ApplyTrustLevel("Established", Late);
        standing.UpdatedAt.Should().Be(Late);

        standing.ApplyContributorApproval(isApproved: true, Early);

        standing.UpdatedAt.Should().Be(Late,
            "it reports the newest fact applied, not the most recent write");
    }

    [Fact]
    public void A_blank_trust_level_is_stored_as_nothing_at_all()
    {
        var standing = NewStanding();

        standing.ApplyTrustLevel("  Established  ", Early);
        standing.TrustLevel.Should().Be("Established");

        standing.ApplyTrustLevel("   ", Late);
        standing.TrustLevel.Should().BeNull();
    }

    // ── Badges ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_badge_is_identified_by_the_pair_of_badge_and_country()
    {
        var standing = NewStanding();

        standing.AwardBadge("Community Guide", "DE", Early);
        standing.AwardBadge("Community Guide", "FR", Early);

        standing.Badges.Should().HaveCount(2,
            "the same badge for Germany and for France are two facts, awarded independently");
    }

    [Fact]
    public void A_redelivered_award_lands_on_the_existing_row_rather_than_duplicating_it()
    {
        var standing = NewStanding();

        standing.AwardBadge("Community Guide", "DE", Early);
        standing.AwardBadge("Community Guide", "DE", Early);

        standing.Badges.Should().ContainSingle()
            .Which.AwardedAt.Should().Be(Early);
    }

    [Fact]
    public void A_newer_award_replaces_an_older_one_for_the_same_pair()
    {
        var standing = NewStanding();

        standing.AwardBadge("Community Guide", "DE", Early);
        standing.AwardBadge("Community Guide", "DE", Late);

        standing.Badges.Should().ContainSingle().Which.AwardedAt.Should().Be(Late);
    }

    [Fact]
    public void A_revocation_removes_the_badge_it_names()
    {
        var standing = NewStanding();

        standing.AwardBadge("Community Guide", "DE", Early);
        standing.AwardBadge("Community Guide", "FR", Early);

        standing.RevokeBadge("Community Guide", "DE", Late).IsSuccess.Should().BeTrue();

        standing.Badges.Should().ContainSingle()
            .Which.CountryCode.Should().Be("FR", "revoking Germany does not revoke France");
    }

    [Fact]
    public void A_stale_revocation_cannot_remove_a_badge_re_awarded_after_it_was_issued()
    {
        // Under unordered delivery, a stored award newer than the revocation means exactly this:
        // the badge was granted again after this revocation was written.
        var standing = NewStanding();

        standing.AwardBadge("Community Guide", "DE", Late);

        standing.RevokeBadge("Community Guide", "DE", Early).IsSuccess.Should().BeTrue();

        standing.Badges.Should().ContainSingle(
            "a re-award that arrived first must not be undone by the revocation it superseded");
    }

    [Fact]
    public void Revoking_a_badge_nobody_holds_is_a_no_op_rather_than_an_error()
    {
        var standing = NewStanding();

        standing.RevokeBadge("Community Guide", "DE", Late).IsSuccess.Should().BeTrue();
        standing.Badges.Should().BeEmpty();
    }

    [Fact]
    public void A_nameless_badge_is_ignored_by_both_paths()
    {
        var standing = NewStanding();

        standing.AwardBadge("   ", "DE", Early).IsSuccess.Should().BeTrue();
        standing.AwardBadge(null, "DE", Early).IsSuccess.Should().BeTrue();
        standing.RevokeBadge(null, "DE", Early).IsSuccess.Should().BeTrue();

        standing.Badges.Should().BeEmpty();
        standing.UpdatedAt.Should().BeNull("nothing was actually applied");
    }

    private static GuideAuthorStanding NewStanding() =>
        GuideAuthorStanding.Create(GuideAuthorStandingId.New(), GuideLifecycleTests.Author).Value;
}
