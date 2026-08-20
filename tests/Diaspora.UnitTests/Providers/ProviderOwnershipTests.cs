using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Providers;

/// <summary>
/// The ownership invariants the provider aggregate is responsible for.
/// <para>
/// These are the rules that decide who can take a business away from whom, so they are the ones
/// worth the most tests. Every case here is a way a naive implementation loses a provider or
/// hands it to the wrong person.
/// </para>
/// </summary>
public class ProviderOwnershipTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Owner = UserId.From(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    private static readonly UserId Manager = UserId.From(Guid.Parse("00000000-0000-0000-0000-0000000000a2"));
    private static readonly UserId Editor = UserId.From(Guid.Parse("00000000-0000-0000-0000-0000000000a3"));
    private static readonly UserId Stranger = UserId.From(Guid.Parse("00000000-0000-0000-0000-0000000000a4"));

    [Fact]
    public void Registration_makes_the_creator_the_owner()
    {
        var provider = Register();

        provider.Members.Should().ContainSingle()
            .Which.Role.Should().Be(ProviderRole.Owner);

        provider.PrimaryOwnerId.Should().Be(Owner);
        provider.Statistics.MemberCount.Should().Be(1);
    }

    [Fact]
    public void A_registered_provider_is_a_draft_and_not_discoverable()
    {
        var provider = Register();

        provider.Status.Should().Be(ProviderStatus.Draft);
        provider.IsDiscoverable.Should().BeFalse();
        provider.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void Nobody_may_hand_out_authority_they_do_not_hold()
    {
        var provider = Register();

        provider.AddMember(NewMembership(), Manager, ProviderRole.Manager, Now, Owner)
            .IsSuccess.Should().BeTrue();

        // A manager appointing an owner would be privilege escalation dressed as an invitation.
        var escalation = provider.AddMember(
            NewMembership(), Editor, ProviderRole.Owner, Now, Manager);

        escalation.IsFailure.Should().BeTrue();
        escalation.Error.Should().Be(ProviderErrors.MemberRoleInsufficient);
    }

    [Fact]
    public void An_editor_may_not_add_anybody_at_all()
    {
        var provider = Register();

        provider.AddMember(NewMembership(), Editor, ProviderRole.Editor, Now, Owner)
            .IsSuccess.Should().BeTrue();

        var refused = provider.AddMember(
            NewMembership(), Stranger, ProviderRole.Contributor, Now, Editor);

        refused.IsFailure.Should().BeTrue();
        refused.Error.Should().Be(ProviderErrors.MemberRoleInsufficient);
    }

    [Fact]
    public void The_last_owner_cannot_be_removed()
    {
        var provider = Register();

        var removed = provider.RemoveMember(Owner, Owner);

        removed.IsFailure.Should().BeTrue();
        removed.Error.Should().Be(ProviderErrors.MemberLastOwner);
        provider.Members.Should().ContainSingle();
    }

    [Fact]
    public void The_last_owner_cannot_be_demoted()
    {
        var provider = Register();

        provider.AddMember(NewMembership(), Manager, ProviderRole.Manager, Now, Owner);

        var demoted = provider.ChangeMemberRole(Owner, ProviderRole.Manager, Owner);

        demoted.IsFailure.Should().BeTrue();
        demoted.Error.Should().Be(ProviderErrors.MemberLastOwner);
    }

    [Fact]
    public void An_owner_may_leave_once_another_owner_exists()
    {
        var provider = Register();

        provider.AddMember(NewMembership(), Manager, ProviderRole.Owner, Now, Owner)
            .IsSuccess.Should().BeTrue();

        provider.RemoveMember(Owner, Owner).IsSuccess.Should().BeTrue();

        provider.Members.Should().ContainSingle().Which.UserId.Should().Be(Manager);
        provider.PrimaryOwnerId.Should().Be(Manager);
    }

    [Fact]
    public void Reducing_your_own_authority_is_refused()
    {
        var provider = Register();

        provider.AddMember(NewMembership(), Manager, ProviderRole.Manager, Now, Owner);

        // An owner who demotes themselves by mistake may have no way back. There is always
        // another owner or a manager who can do it deliberately.
        var demoted = provider.ChangeMemberRole(Manager, ProviderRole.Editor, Manager);

        demoted.IsFailure.Should().BeTrue();
        demoted.Error.Should().Be(ProviderErrors.MemberSelfDemotion);
    }

    [Fact]
    public void Somebody_may_always_remove_themselves()
    {
        var provider = Register();

        provider.AddMember(NewMembership(), Editor, ProviderRole.Editor, Now, Owner);

        provider.RemoveMember(Editor, Editor).IsSuccess.Should().BeTrue();

        provider.Members.Should().ContainSingle().Which.UserId.Should().Be(Owner);
    }

    [Fact]
    public void Adding_the_same_person_twice_is_refused()
    {
        var provider = Register();

        provider.AddMember(NewMembership(), Manager, ProviderRole.Manager, Now, Owner);

        var duplicate = provider.AddMember(
            NewMembership(), Manager, ProviderRole.Editor, Now, Owner);

        duplicate.IsFailure.Should().BeTrue();
        duplicate.Error.Should().Be(ProviderErrors.MemberDuplicate);
    }

    [Fact]
    public void Transferring_ownership_keeps_the_previous_owner_as_a_manager_by_default()
    {
        var provider = Register();

        provider.TransferOwnership(Manager, Owner, Now, NewMembership())
            .IsSuccess.Should().BeTrue();

        provider.RoleOf(Manager).Should().Be(ProviderRole.Owner);
        provider.RoleOf(Owner).Should().Be(ProviderRole.Manager);
        provider.PrimaryOwnerId.Should().Be(Manager);
    }

    [Fact]
    public void Transferring_ownership_can_remove_the_previous_owner_entirely()
    {
        var provider = Register();

        provider.TransferOwnership(Manager, Owner, Now, NewMembership(), retainPreviousOwner: false)
            .IsSuccess.Should().BeTrue();

        provider.RoleOf(Owner).Should().BeNull();
        provider.Members.Should().ContainSingle().Which.UserId.Should().Be(Manager);
    }

    [Fact]
    public void Only_an_owner_may_transfer_ownership()
    {
        var provider = Register();

        provider.AddMember(NewMembership(), Manager, ProviderRole.Manager, Now, Owner);

        var refused = provider.TransferOwnership(Stranger, Manager, Now, NewMembership());

        refused.IsFailure.Should().BeTrue();
        refused.Error.Should().Be(ProviderErrors.MemberRoleInsufficient);
    }

    [Fact]
    public void Roles_are_ordered_so_a_single_comparison_answers_every_authority_question()
    {
        var provider = Register();

        provider.AddMember(NewMembership(), Editor, ProviderRole.Editor, Now, Owner);

        provider.HasAtLeast(Editor, ProviderRole.Contributor).Should().BeTrue();
        provider.HasAtLeast(Editor, ProviderRole.Editor).Should().BeTrue();
        provider.HasAtLeast(Editor, ProviderRole.Manager).Should().BeFalse();
        provider.HasAtLeast(Stranger, ProviderRole.Contributor).Should().BeFalse();
        provider.HasAtLeast(null, ProviderRole.Contributor).Should().BeFalse();
    }

    internal static Provider Register(ProviderType type = ProviderType.Business)
    {
        var name = ProviderName.Create("Mama Afrika Kitchen");

        return Provider.Register(
            ProviderId.New(),
            name.Value,
            ProviderSlug.FromName(name.Value),
            type,
            ServiceCategoryId.New(),
            Owner,
            ProviderProfileId.New(),
            ProviderMembershipId.New(),
            Now).Value;
    }

    private static ProviderMembershipId NewMembership() => ProviderMembershipId.New();
}
