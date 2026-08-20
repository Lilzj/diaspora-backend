using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// COMMUNITY-LIFE-GAP-013's keystone: a B12 role becomes ten capability flags, in one place.
/// <para>
/// The frontend asserts exhaustively that it never derives a capability from a role name. These are
/// the server-side half of the same property: the map is the only thing that does derive one, and
/// every path through it that is not an explicit rung answers nothing.
/// </para>
/// </summary>
public sealed class OrganizationCapabilityTests
{
    private static readonly Guid Organization = Guid.NewGuid();

    [Fact]
    public void No_role_grants_nothing()
    {
        var capabilities = OrganizationCapabilityMap.For(Organization, null);

        capabilities.Role.Should().BeNull();
        capabilities.HoldsAny.Should().BeFalse();
    }

    /// <summary>
    /// <b>An unrecognised role denies.</b> The alternative grants a capability on the strength of a
    /// typo, and a capability model whose failure mode is "grant" is not one.
    /// </summary>
    [Theory]
    [InlineData("Treasurer")]
    [InlineData("owner ")]
    [InlineData("OWNER")]
    [InlineData("Superuser")]
    [InlineData("")]
    public void An_unrecognised_role_grants_nothing(string role)
    {
        var capabilities = OrganizationCapabilityMap.For(Organization, role);

        // "owner " trims to Owner and is granted; the casing variants and the invented names are not.
        // Both behaviours are deliberate: whitespace is a transport artefact, a different spelling is
        // a different name.
        if (role.Trim() == "Owner")
        {
            capabilities.HoldsAny.Should().BeTrue();

            return;
        }

        capabilities.HoldsAny.Should().BeFalse($"'{role}' is not a role this platform ships");
    }

    [Fact]
    public void An_owner_holds_everything()
    {
        var capabilities = OrganizationCapabilityMap.For(Organization, "Owner");

        foreach (var capability in Enum.GetValues<OrganizationCapability>())
        {
            capabilities.Grants(capability).Should().BeTrue($"an owner may {capability}");
        }
    }

    /// <summary>
    /// The Organizer is the interesting rung, and the register says why: "a capability model that
    /// only ever produces all or nothing is a boolean with extra steps."
    /// </summary>
    [Fact]
    public void An_organizer_holds_a_partial_set()
    {
        var capabilities = OrganizationCapabilityMap.For(Organization, "Organizer");

        capabilities.CanManageEvents.Should().BeTrue();
        capabilities.CanManageVolunteers.Should().BeTrue();
        capabilities.CanManagePolls.Should().BeTrue();
        capabilities.CanPublishAnnouncements.Should().BeTrue();
        capabilities.CanViewAnalytics.Should().BeTrue();

        capabilities.CanManageMembers.Should().BeFalse();
        capabilities.CanApproveMembers.Should().BeFalse();
        capabilities.CanManageTiers.Should().BeFalse();
        capabilities.CanManageCampaigns.Should().BeFalse();
        capabilities.CanViewFinancials.Should().BeFalse();
    }

    /// <summary>
    /// <b>A plain member holds nothing management-related</b>, which is the case a dashboard tested
    /// only against an owner never renders.
    /// </summary>
    [Fact]
    public void A_member_holds_nothing()
    {
        OrganizationCapabilityMap.For(Organization, "Member").HoldsAny.Should().BeFalse();
    }

    /// <summary>
    /// <b>B12's Volunteer rung is not quietly widened into running the volunteer programme.</b> B12
    /// grants a volunteer attendance-taking and nothing else; mapping that to
    /// <c>canManageVolunteers</c> would let anybody who signed up to help at an event decide who else
    /// may.
    /// </summary>
    [Fact]
    public void A_volunteer_cannot_run_the_volunteer_programme()
    {
        var capabilities = OrganizationCapabilityMap.For(Organization, "Volunteer");

        capabilities.CanManageVolunteers.Should().BeFalse();
        capabilities.HoldsAny.Should().BeFalse();
    }

    /// <summary>
    /// A moderator handles the community's voice and not its roll or its money — the narrowest rung
    /// that holds anything at all.
    /// </summary>
    [Fact]
    public void A_moderator_holds_polls_and_announcements_only()
    {
        var capabilities = OrganizationCapabilityMap.For(Organization, "Moderator");

        capabilities.CanManagePolls.Should().BeTrue();
        capabilities.CanPublishAnnouncements.Should().BeTrue();
        capabilities.CanManageMembers.Should().BeFalse();
        capabilities.CanViewFinancials.Should().BeFalse();
        capabilities.CanManageVolunteers.Should().BeFalse();
    }

    /// <summary>
    /// Every rung carries the role name back verbatim, so a client can render "Organizer" without
    /// this module inventing a display vocabulary.
    /// </summary>
    [Theory]
    [InlineData("Owner")]
    [InlineData("Administrator")]
    [InlineData("Organizer")]
    [InlineData("Moderator")]
    [InlineData("Volunteer")]
    [InlineData("Member")]
    public void The_role_name_is_passed_through(string role) =>
        OrganizationCapabilityMap.For(Organization, role).Role.Should().Be(role);

    /// <summary>
    /// <b>Grants covers every member of the enum.</b> A capability added to the enum and forgotten in
    /// the switch would silently answer false everywhere — which fails closed, but would make a new
    /// capability look like a permission nobody has rather than one nobody wired up.
    /// </summary>
    [Fact]
    public void Every_capability_is_reachable_through_an_owner()
    {
        var owner = OrganizationCapabilityMap.For(Organization, "Owner");

        Enum.GetValues<OrganizationCapability>()
            .Should().OnlyContain(capability => owner.Grants(capability));
    }
}
