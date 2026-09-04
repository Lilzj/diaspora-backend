using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.Modules.CommunityProgramme;
// `Diaspora.UnitTests.Community` is a sibling namespace, so the bare name resolves to it
// rather than to the aggregate. Aliased once here instead of qualifying every use.
using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// The community aggregate — the entity twenty-three register entries were waiting on.
/// <para>
/// These pin the rules the register named explicitly: a stable identifier and a stable slug, a
/// visibility model where private is indistinguishable from absent, and a lifecycle where suspended
/// and archived are different things. Anything a later phase reads — the directory, the profile, the
/// join path — reads it through these.
/// </para>
/// </summary>
public class CommunityAggregateTests
{
    // ── The slug ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Nigerians in London", "nigerians-in-london")]
    [InlineData("  Ghanaians   in  Toronto  ", "ghanaians-in-toronto")]
    [InlineData("Côte d'Ivoire Diaspora", "cote-d-ivoire-diaspora")]
    [InlineData("Yorùbá Speakers", "yoruba-speakers")]
    [InlineData("West Africa — UK", "west-africa-uk")]
    public void A_name_becomes_a_readable_handle(string name, string expected)
    {
        // Accents fold to their base letter rather than percent-encoding, so the handle can be read
        // aloud down a phone.
        CommunitySlug.FromName(name).Value.Value.Should().Be(expected);
    }

    [Fact]
    public void A_name_with_nothing_sluggable_is_refused_rather_than_transliterated()
    {
        // A community named only in Arabic or Amharic needs an explicit handle. Inventing a
        // transliteration is not something this module is qualified to do.
        var result = CommunitySlug.FromName("العربية");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CommunityErrorCodes.SlugNotDerivable);
    }

    [Fact]
    public void A_supplied_handle_is_read_strictly_rather_than_normalised()
    {
        // A caller sending a display name to a lookup route has a bug. Answering it as though it
        // were the slug hides that bug until two slugs collide.
        CommunitySlug.Create("Nigerians In London").IsFailure.Should().BeTrue();
        CommunitySlug.Create("nigerians-in-london").IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("me")]
    [InlineData("search")]
    [InlineData("directory")]
    [InlineData("memberships")]
    public void A_handle_that_would_shadow_a_route_is_refused(string reserved)
    {
        // Refused, not silently suffixed: "me-2" is a handle nobody chose.
        var result = CommunitySlug.Create(reserved);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CommunityErrorCodes.SlugReserved);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    [InlineData("Upper")]
    [InlineData("has space")]
    public void A_handle_outside_the_scheme_is_refused(string candidate)
    {
        CommunitySlug.Create(candidate).IsFailure.Should().BeTrue();
    }

    // ── Creation ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_community_opens_pending_rather_than_live()
    {
        // A community that appeared in the directory the instant somebody typed a name would make
        // the directory a scratchpad.
        var community = Proposed();

        community.Status.Should().Be(CommunityStatus.Pending);
        community.IsJoinable.Should().BeFalse();
        community.IsListable.Should().BeFalse();
    }

    [Fact]
    public void A_national_community_without_an_origin_is_refused()
    {
        // The directory's central filter is origin. A row that cannot answer it is invisible in the
        // one place people look.
        var result = CommunityAggregate.Propose(
            CommunityId.New(), Slug("rootless"), "Rootless",
            CommunityType.NationalDiaspora, CommunityVisibility.Public);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CommunityErrorCodes.OriginRequired);
    }

    [Fact]
    public void An_interest_community_needs_no_origin_at_all()
    {
        // The kind is not the origin — that separation is why the directory can ask two different
        // questions without them fighting.
        CommunityAggregate.Propose(
            CommunityId.New(), Slug("book-club"), "Book Club",
            CommunityType.InterestCommunity, CommunityVisibility.Public)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_region_satisfies_the_origin_requirement_where_no_country_does()
    {
        // "West Africa" is not a country code, and a regional diaspora is exactly the case that
        // needs it.
        CommunityAggregate.Propose(
            CommunityId.New(), Slug("west-africa-uk"), "West Africans in the UK",
            CommunityType.RegionalDiaspora, CommunityVisibility.Public,
            originRegionName: "West Africa")
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_country_code_is_normalised_and_a_malformed_one_refused()
    {
        var community = CommunityAggregate.Propose(
            CommunityId.New(), Slug("nigerians"), "Nigerians",
            CommunityType.NationalDiaspora, CommunityVisibility.Public, "ng").Value;

        community.OriginCountryCode.Should().Be("NG");

        CommunityAggregate.Propose(
            CommunityId.New(), Slug("bad"), "Bad",
            CommunityType.NationalDiaspora, CommunityVisibility.Public, "NGA")
            .Error.Code.Should().Be(CommunityErrorCodes.CountryCodeShape);
    }

    // ── The handle survives a rename ────────────────────────────────────────────────────────

    [Fact]
    public void Renaming_leaves_the_handle_alone()
    {
        // The whole point of a stable slug: a link somebody sent last year keeps working.
        var community = Proposed();
        var original = community.Slug.Value;

        community.Rename("Nigerians in Greater London").IsSuccess.Should().BeTrue();

        community.Name.Should().Be("Nigerians in Greater London");
        community.Slug.Value.Should().Be(original);
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Opening_makes_a_community_joinable_and_listable()
    {
        var community = Proposed();

        community.Open().IsSuccess.Should().BeTrue();

        community.Status.Should().Be(CommunityStatus.Active);
        community.IsJoinable.Should().BeTrue();
        community.IsListable.Should().BeTrue();
    }

    [Fact]
    public void Suspending_without_a_reason_is_refused()
    {
        // Members are shown the reason verbatim, so there has to be one.
        var community = Opened();

        community.Suspend("  ").Error.Code.Should().Be(CommunityErrorCodes.SuspensionNeedsReason);
    }

    [Fact]
    public void A_suspended_community_stays_listed_but_cannot_be_joined()
    {
        // Members keep their standing and can still find it; nobody new gets in. That is the whole
        // difference from archived.
        var community = Opened();

        community.Suspend("Under review after a report.").IsSuccess.Should().BeTrue();

        community.Status.Should().Be(CommunityStatus.Suspended);
        community.StatusReason.Should().Be("Under review after a report.");
        community.IsJoinable.Should().BeFalse();
        community.IsListable.Should().BeTrue();
    }

    [Fact]
    public void A_suspension_is_reversible_and_clears_its_reason()
    {
        var community = Opened();
        community.Suspend("Under review.");

        community.Open().IsSuccess.Should().BeTrue();

        community.Status.Should().Be(CommunityStatus.Active);
        community.StatusReason.Should().BeNull();
    }

    [Fact]
    public void Archiving_is_final_in_both_directions()
    {
        // The register asked for archived to be distinguishable from deleted. It stays readable —
        // links resolve to an explanation rather than a 404 — and it never reopens.
        var community = Opened();
        community.Archive("Merged into Nigerians in the UK.");

        community.Status.Should().Be(CommunityStatus.Archived);
        community.Open().Error.Code.Should().Be(CommunityErrorCodes.ArchivedIsFinal);
        community.Suspend("anything").Error.Code.Should().Be(CommunityErrorCodes.ArchivedIsFinal);
    }

    [Fact]
    public void Archiving_twice_is_not_an_error()
    {
        var community = Opened();
        community.Archive("Merged.");

        community.Archive("Merged again.").IsSuccess.Should().BeTrue();
    }

    // ── Visibility ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CommunityVisibility.Unlisted)]
    [InlineData(CommunityVisibility.Private)]
    [InlineData(CommunityVisibility.InviteOnly)]
    public void Only_a_public_community_is_ever_listed(CommunityVisibility visibility)
    {
        // An unlisted community is fully readable by anyone holding its link and merely kept out of
        // the directory; a private one is readable by members only. Neither is listable.
        var community = Opened();
        community.SetVisibility(visibility);

        community.IsListable.Should().BeFalse();
        community.IsJoinable.Should().BeTrue("visibility decides who sees it, not whether it is open");
    }

    // ── Presence ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Presence_is_normalised_and_deduplicated()
    {
        // Where a community is present is not where it is from: one origin, many presences.
        var community = Opened();

        community.SetPresence(["gb", "GB", " ca ", "ae"]).IsSuccess.Should().BeTrue();

        community.PresenceCountryCodes.Should().BeEquivalentTo(["GB", "CA", "AE"]);
        community.OriginCountryCode.Should().Be("NG", "presence never overwrites origin");
    }

    [Fact]
    public void A_malformed_presence_code_is_refused_and_changes_nothing()
    {
        var community = Opened();
        community.SetPresence(["GB"]);

        community.SetPresence(["GB", "NGA"]).IsFailure.Should().BeTrue();

        community.PresenceCountryCodes.Should().BeEquivalentTo(["GB"]);
    }

    // ── Member count ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_member_count_is_moved_only_by_joining_and_leaving()
    {
        var community = Opened();

        community.RecordMemberJoined();
        community.RecordMemberJoined();
        community.RecordMemberLeft();

        community.MemberCount.Should().Be(1);
    }

    [Fact]
    public void The_member_count_floors_at_zero_rather_than_going_negative()
    {
        // A negative count is a reporting bug. Taking down somebody's leave request over it helps
        // nobody, so it floors and the discrepancy surfaces in reconciliation instead.
        var community = Opened();

        community.RecordMemberLeft();

        community.MemberCount.Should().Be(0);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static CommunitySlug Slug(string value) => CommunitySlug.Create(value).Value;

    private static CommunityAggregate Proposed() =>
        CommunityAggregate.Propose(
            CommunityId.New(), Slug("nigerians-in-london"), "Nigerians in London",
            CommunityType.NationalDiaspora, CommunityVisibility.Public, "NG").Value;

    private static CommunityAggregate Opened()
    {
        var community = Proposed();
        community.Open();
        return community;
    }
}
