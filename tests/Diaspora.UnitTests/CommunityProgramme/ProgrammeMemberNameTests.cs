using Diaspora.Modules.CommunityProgramme.Application.Mapping;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// What the organisation roll calls a member.
/// <para>
/// <b>The client rendered the identifier as the person.</b> <c>OrganizationMemberResponse</c>
/// carried no name, so the adapter mapped <c>displayName: wire.userId</c> — and the member list,
/// the accessibility labels and the alert an administrator reads before declining somebody all
/// said "3f2a…". A comment above it argued the platform sent no name and inventing one would be
/// worse. Both true, and neither the only option: <c>IMemberProfileService</c> is the sanctioned
/// batch source, and the community members route already used it.
/// </para>
/// <para>
/// Null stays a real answer — the platform saying it cannot tell us who this is — and the client
/// renders a neutral label for it. What must never appear is the identifier wearing the name's
/// place, which is what these tests hold.
/// </para>
/// </summary>
public sealed class ProgrammeMemberNameTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_resolved_name_is_the_name()
    {
        var membership = Membership(out var member);

        var response = CommunityProgrammeMapper.ToRollResponse(
            membership, tier: null, organizationMembership: null, Names(member, "Joshua Gbogodor"));

        response.DisplayName.Should().Be("Joshua Gbogodor");
    }

    /// <summary>
    /// The defect, stated as an assertion. An unresolved name is null — not the identifier, and
    /// not anything derived from it.
    /// </summary>
    [Fact]
    public void An_unresolved_name_is_null_and_never_the_identifier()
    {
        var membership = Membership(out var member);

        var response = CommunityProgrammeMapper.ToRollResponse(
            membership, tier: null, organizationMembership: null, NoNames);

        response.DisplayName.Should().BeNull();
        response.UserId.Should().Be(member.Value, "the identifier still travels, in its own field");
    }

    /// <summary>
    /// A member the batch answered for and a member it did not, side by side. The one that was
    /// answered must not lend its name to the one that was not.
    /// </summary>
    [Fact]
    public void One_resolved_name_does_not_stand_in_for_another_member()
    {
        var named = Membership(out var first);
        var unnamed = Membership(out _);

        var names = Names(first, "Joshua Gbogodor");

        CommunityProgrammeMapper
            .ToRollResponse(named, null, null, names)
            .DisplayName.Should().Be("Joshua Gbogodor");

        CommunityProgrammeMapper
            .ToRollResponse(unnamed, null, null, names)
            .DisplayName.Should().BeNull();
    }

    [Fact]
    public void The_applicant_list_answers_the_same_way()
    {
        var applicant = UserId.From(Guid.NewGuid());

        // Both routes read the same helper, so the rule cannot hold on one and not the other.
        var resolved = CommunityProgrammeMapper.ToRollResponse(
            Membership(applicant), null, null, Names(applicant, "Amara Kone"));

        resolved.DisplayName.Should().Be("Amara Kone");

        CommunityProgrammeMapper
            .ToRollResponse(Membership(applicant), null, null, NoNames)
            .DisplayName.Should().BeNull();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static Dictionary<Guid, MemberProfile> NoNames => [];

    private static Dictionary<Guid, MemberProfile> Names(UserId member, string name) =>
        new Dictionary<Guid, MemberProfile>
        {
            [member.Value] = new() { UserId = member.Value, DisplayName = name }
        };

    private static ProgrammeMembership Membership(out UserId member)
    {
        member = UserId.From(Guid.NewGuid());
        return Membership(member);
    }

    private static ProgrammeMembership Membership(UserId member) =>
        ProgrammeMembership.Apply(
            ProgrammeMembershipId.New(),
            Guid.NewGuid(),
            MembershipTierId.New(),
            member,
            ProgrammeMembershipStatus.Approved,
            Guid.NewGuid(),
            Now,
            "application-key").Value;
}
