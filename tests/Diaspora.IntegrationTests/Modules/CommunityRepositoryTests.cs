using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Domain.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The community repositories, against a real database.
/// <para>
/// <b>Two of the rules in this phase live in indexes, not in code.</b> A <c>CommunitySlug</c> cannot
/// see other rows, so it can guarantee a handle's shape and nothing about uniqueness; a
/// <c>CommunityMembership</c> cannot see the member's other memberships, so it cannot enforce one
/// primary. Both are database constraints, and a unit test cannot reach either — the only way to
/// know they hold is to try to violate them.
/// </para>
/// <para>
/// <b>The directory's visibility filter is here for the same reason.</b> Forgetting it does not
/// throw; it leaks the existence of every private community on the platform, and it leaks it as a
/// perfectly ordinary 200.
/// </para>
/// </summary>
public sealed class CommunityRepositoryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CommunityRepositoryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;

        // The schema is created on demand rather than at host start — the provider is not
        // resolvable at either point the minimal hosting model offers. A fixture that skips this
        // gets "no such table" on its first write and passes every assertion that expected an
        // absence, which reads as a suite that mostly works.
        _factory.EnsureDatabaseCreated();
    }

    // ── The slug index ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_communities_cannot_hold_the_same_handle()
    {
        // Without this the second one silently stops resolving, which is the failure the whole
        // stable-slug requirement exists to prevent.
        var slug = UniqueSlug("dup");

        await WriteAsync(async (communities, _, unitOfWork) =>
        {
            communities.Add(Proposed(slug, "First"));
            await unitOfWork.SaveChangesAsync();
        });

        var second = async () => await WriteAsync(async (communities, _, unitOfWork) =>
        {
            communities.Add(Proposed(slug, "Second"));
            await unitOfWork.SaveChangesAsync();
        });

        await second.Should().ThrowAsync<Exception>(
            "the unique index is what actually stops it, not the value object");
    }

    [Fact]
    public async Task A_taken_handle_is_reported_before_the_write_is_attempted()
    {
        // So a caller gets a refusal it can render, rather than a database exception.
        var slug = UniqueSlug("taken");

        await WriteAsync(async (communities, _, unitOfWork) =>
        {
            communities.Add(Proposed(slug, "Held"));
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async (communities, _) =>
        {
            (await communities.IsSlugTakenAsync(slug)).Should().BeTrue();
            (await communities.IsSlugTakenAsync(UniqueSlug("free"))).Should().BeFalse();
        });
    }

    // ── Lookup by either handle or id ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_community_is_found_by_its_id_and_by_its_handle()
    {
        var slug = UniqueSlug("either");
        var community = Proposed(slug, "Either Way");

        await WriteAsync(async (communities, _, unitOfWork) =>
        {
            communities.Add(community);
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async (communities, _) =>
        {
            (await communities.FindByIdOrSlugAsync(community.Id.Value.ToString()))
                .Should().NotBeNull().And.Subject.As<CommunityAggregate>()
                .Slug.Value.Should().Be(slug.Value);

            (await communities.FindByIdOrSlugAsync(slug.Value))
                .Should().NotBeNull().And.Subject.As<CommunityAggregate>()
                .Id.Should().Be(community.Id);
        });
    }

    [Fact]
    public async Task A_handle_that_could_never_exist_reads_as_absent_rather_than_throwing()
    {
        // "Not a valid handle" and "no such community" are the same answer to a caller, and the
        // handler should not have to distinguish them.
        await ReadAsync(async (communities, _) =>
        {
            (await communities.FindByIdOrSlugAsync("Not A Valid Slug")).Should().BeNull();
            (await communities.FindByIdOrSlugAsync("")).Should().BeNull();
        });
    }

    // ── The directory's visibility filter ───────────────────────────────────────────────────

    [Fact]
    public async Task The_directory_lists_only_public_live_communities()
    {
        var marker = $"vis{Guid.NewGuid():N}"[..12];

        await WriteAsync(async (communities, _, unitOfWork) =>
        {
            var listed = Opened(UniqueSlug("pub"), $"{marker} Public");

            var unlisted = Opened(UniqueSlug("unl"), $"{marker} Unlisted");
            unlisted.SetVisibility(CommunityVisibility.Unlisted);

            var priv = Opened(UniqueSlug("prv"), $"{marker} Private");
            priv.SetVisibility(CommunityVisibility.Private);

            // Proposed and never opened.
            var pending = Proposed(UniqueSlug("pend"), $"{marker} Pending");

            var suspended = Opened(UniqueSlug("susp"), $"{marker} Suspended");
            suspended.Suspend("Under review.");

            foreach (var community in new[] { listed, unlisted, priv, pending, suspended })
            {
                communities.Add(community);
            }

            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async (communities, _) =>
        {
            var page = await communities.SearchAsync(new CommunityDirectoryFilter { Query = marker });
            var names = page.Items.Select(community => community.Name).ToList();

            names.Should().Contain($"{marker} Public");

            // Suspended stays listed so members can still find a community they belong to and read
            // why it stopped.
            names.Should().Contain($"{marker} Suspended");

            names.Should().NotContain($"{marker} Unlisted", "unlisted means readable by link only");
            names.Should().NotContain($"{marker} Private", "private answers exactly as absent");
            names.Should().NotContain($"{marker} Pending", "the directory is not a scratchpad");
        });
    }

    [Fact]
    public async Task The_directory_filters_on_origin_and_on_presence_separately()
    {
        // Where a community is from and where it has a presence are two different questions, and
        // keeping them apart is what lets the directory ask both.
        var marker = $"org{Guid.NewGuid():N}"[..12];

        await WriteAsync(async (communities, _, unitOfWork) =>
        {
            var nigerianInUk = Opened(UniqueSlug("ng-uk"), $"{marker} Nigerians UK", "NG");
            nigerianInUk.SetPresence(["GB"]);

            var ghanaianInCanada = Opened(UniqueSlug("gh-ca"), $"{marker} Ghanaians CA", "GH");
            ghanaianInCanada.SetPresence(["CA"]);

            communities.Add(nigerianInUk);
            communities.Add(ghanaianInCanada);
            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async (communities, _) =>
        {
            var byOrigin = await communities.SearchAsync(
                new CommunityDirectoryFilter { Query = marker, OriginCountryCodes = ["NG"] });

            byOrigin.Items.Should().ContainSingle()
                .Which.Name.Should().Be($"{marker} Nigerians UK");

            var byPresence = await communities.SearchAsync(
                new CommunityDirectoryFilter { Query = marker, PresenceCountryCode = "ca" });

            byPresence.Items.Should().ContainSingle()
                .Which.Name.Should().Be($"{marker} Ghanaians CA");
        });
    }

    // ── The one-primary index ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_member_cannot_hold_two_primary_communities()
    {
        // Nothing above the database can prevent this: a membership cannot see the member's other
        // memberships. Two primaries would make "your community" ambiguous on a profile.
        var member = UserId.New();

        var attempt = async () => await WriteAsync(async (communities, memberships, unitOfWork) =>
        {
            foreach (var suffix in new[] { "one", "two" })
            {
                var community = Opened(UniqueSlug(suffix), $"Primary {suffix}");
                communities.Add(community);

                var membership = CommunityMembership.Start(
                    CommunityMembershipId.New(), community.Id, member,
                    CommunityMembershipState.Following).Value;

                membership.Join(community, requiresApproval: false, DateTimeOffset.UtcNow);
                membership.MakePrimary();
                memberships.Add(membership);
            }

            await unitOfWork.SaveChangesAsync();
        });

        await attempt.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task One_row_per_member_per_community()
    {
        // The reason a row is kept rather than deleted: leaving and rejoining must not launder a
        // revocation by creating a second row.
        var member = UserId.New();
        var community = Opened(UniqueSlug("once"), "Only Once");

        await WriteAsync(async (communities, memberships, unitOfWork) =>
        {
            communities.Add(community);
            memberships.Add(CommunityMembership.Start(
                CommunityMembershipId.New(), community.Id, member,
                CommunityMembershipState.Following).Value);
            await unitOfWork.SaveChangesAsync();
        });

        var second = async () => await WriteAsync(async (_, memberships, unitOfWork) =>
        {
            memberships.Add(CommunityMembership.Start(
                CommunityMembershipId.New(), community.Id, member,
                CommunityMembershipState.Interested).Value);
            await unitOfWork.SaveChangesAsync();
        });

        await second.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task A_members_standing_is_readable_for_a_whole_page_in_one_query()
    {
        // The directory fills in the caller's own state per row. Doing that one row at a time is the
        // N+1 that makes a directory unusable at the scale §77 requires.
        var member = UserId.New();
        var ids = new List<CommunityId>();

        await WriteAsync(async (communities, memberships, unitOfWork) =>
        {
            foreach (var suffix in new[] { "a", "b", "c" })
            {
                var community = Opened(UniqueSlug(suffix), $"Page {suffix}");
                communities.Add(community);
                ids.Add(community.Id);

                memberships.Add(CommunityMembership.Start(
                    CommunityMembershipId.New(), community.Id, member,
                    CommunityMembershipState.Following).Value);
            }

            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async (_, memberships) =>
        {
            var standing = await memberships.FindForMemberAsync(member, ids);

            standing.Should().HaveCount(3);
            standing.Keys.Should().BeEquivalentTo(ids);
        });
    }

    [Fact]
    public async Task A_row_survives_leaving_and_is_still_found()
    {
        // Every reader has to say which states it counts — the row's existence is not standing.
        var member = UserId.New();
        var community = Opened(UniqueSlug("left"), "Left Behind");

        await WriteAsync(async (communities, memberships, unitOfWork) =>
        {
            communities.Add(community);

            var membership = CommunityMembership.Start(
                CommunityMembershipId.New(), community.Id, member,
                CommunityMembershipState.Following).Value;

            membership.Join(community, requiresApproval: false, DateTimeOffset.UtcNow);
            membership.Leave(DateTimeOffset.UtcNow);
            memberships.Add(membership);

            await unitOfWork.SaveChangesAsync();
        });

        await ReadAsync(async (_, memberships) =>
        {
            var found = await memberships.FindAsync(community.Id, member);

            found.Should().NotBeNull();
            found!.State.Should().Be(CommunityMembershipState.Left);
            found.IsCurrentMember.Should().BeFalse();
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static CommunitySlug UniqueSlug(string prefix) =>
        CommunitySlug.Create($"{prefix}-{Guid.NewGuid():N}"[..24].TrimEnd('-')).Value;

    private static CommunityAggregate Proposed(
        CommunitySlug slug, string name, string origin = "NG") =>
        CommunityAggregate.Propose(
            CommunityId.New(), slug, name,
            CommunityType.NationalDiaspora, CommunityVisibility.Public, origin).Value;

    private static CommunityAggregate Opened(
        CommunitySlug slug, string name, string origin = "NG")
    {
        var community = Proposed(slug, name, origin);
        community.Open();
        return community;
    }

    private async Task WriteAsync(
        Func<ICommunityRepository, ICommunityMembershipRepository, IUnitOfWork, Task> work)
    {
        using var scope = _factory.Services.CreateScope();

        await work(
            scope.ServiceProvider.GetRequiredService<ICommunityRepository>(),
            scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
    }

    private async Task ReadAsync(
        Func<ICommunityRepository, ICommunityMembershipRepository, Task> work)
    {
        using var scope = _factory.Services.CreateScope();

        await work(
            scope.ServiceProvider.GetRequiredService<ICommunityRepository>(),
            scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>());
    }
}
